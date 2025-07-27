#region Licence

/* The MIT License (MIT)
Copyright © 2014 Francesco Pighi <francesco.pighi@gmail.com>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Net.Mime;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Observability;
using Paramore.Brighter.PostgreSql;

namespace Paramore.Brighter.Outbox.PostgreSql
{
    /// <summary>
    /// Implements an outbox using PostgreSQL as a backing store
    /// </summary>
    public partial class PostgreSqlOutbox : RelationDatabaseOutbox
    {
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<PostgreSqlOutbox>();

        private readonly IAmARelationalDatabaseConfiguration _configuration;
        private readonly IAmARelationalDbConnectionProvider _connectionProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="PostgreSqlOutbox" /> class.
        /// </summary>
        /// <param name="configuration">The configuration to connect to this data store</param>
        /// <param name="connectionProvider">Provides a connection to the Db that allows us to enlist in an ambient transaction</param>
        public PostgreSqlOutbox(
            IAmARelationalDatabaseConfiguration configuration,
            IAmARelationalDbConnectionProvider connectionProvider) : base(
                DbSystem.Postgresql, configuration.DatabaseName, configuration.OutBoxTableName, 
                new PostgreSqlQueries(), ApplicationLogging.CreateLogger<PostgreSqlOutbox>())
        {
            _configuration = configuration;
            _connectionProvider = connectionProvider;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PostgreSqlOutbox" /> class.
        /// </summary>
        /// <param name="configuration">The configuration to connect to this data store</param>
        /// <param name="dataSource">From v7.0 Npgsql uses an Npgsql data source, leave null to have Brighter manage
        /// connections; Brighter will not manage type mapping for you in this case so you must register them
        /// globally</param>
        public PostgreSqlOutbox(
            IAmARelationalDatabaseConfiguration configuration,
            NpgsqlDataSource? dataSource = null)
            : this(configuration, new PostgreSqlConnectionProvider(configuration, dataSource))
        {
        }

        protected override void WriteToStore(
            IAmABoxTransactionProvider<DbTransaction>? transactionProvider,
            Func<DbConnection, DbCommand> commandFunc,
            Action? loggingAction)
        {
            var connection = GetOpenConnection(_connectionProvider, transactionProvider);
            using var command = commandFunc.Invoke(connection);
            try
            {
                if (transactionProvider is { HasOpenTransaction: true })
                    command.Transaction = transactionProvider.GetTransaction();
                command.ExecuteNonQuery();
            }
            catch (PostgresException sqlException)
            {
                if (sqlException.SqlState == PostgresErrorCodes.UniqueViolation)
                {
                    loggingAction?.Invoke();
                    return;
                }

                throw;
            }
            finally
            {
                FinishWrite(connection, transactionProvider);
            }
        }

        protected override async Task WriteToStoreAsync(
            IAmABoxTransactionProvider<DbTransaction>? transactionProvider,
            Func<DbConnection, DbCommand> commandFunc,
            Action? loggingAction,
            CancellationToken cancellationToken)
        {
            var connection = await GetOpenConnectionAsync(_connectionProvider, transactionProvider, cancellationToken);
            await using var command = commandFunc.Invoke(connection);
            try
            {
                if (transactionProvider is { HasOpenTransaction: true })
                    command.Transaction = await transactionProvider.GetTransactionAsync(cancellationToken);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (PostgresException sqlException)
            {
                if (sqlException.SqlState == PostgresErrorCodes.UniqueViolation)
                {
                    Log.DuplicateDetectedInBatch(s_logger);
                    return;
                }

                throw;
            }
            finally
            {
                FinishWrite(connection, transactionProvider);
            }
        }

        protected override T ReadFromStore<T>(
            Func<DbConnection, DbCommand> commandFunc,
            Func<DbDataReader, T> resultFunc
        )
        {
            var connection = _connectionProvider.GetConnection();

            if (connection.State != ConnectionState.Open)
                connection.Open();
            using var command = commandFunc.Invoke(connection);
            try
            {
                return resultFunc.Invoke(command.ExecuteReader());
            }
            finally
            {
                connection.Close();
            }
        }

        protected override async Task<T> ReadFromStoreAsync<T>(
            Func<DbConnection, DbCommand> commandFunc,
            Func<DbDataReader, Task<T>> resultFunc,
            CancellationToken cancellationToken
        )
        {
            var connection = await _connectionProvider.GetConnectionAsync(cancellationToken);

            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync(cancellationToken);
            await using var command = commandFunc.Invoke(connection);
            try
            {
                return await resultFunc.Invoke(await command.ExecuteReaderAsync(cancellationToken));
            }
            finally
            {
                await connection.CloseAsync();
            }
        }

        protected override DbCommand CreateCommand(
            DbConnection connection,
            string sqlText,
            int outBoxTimeout,
            params IDbDataParameter[] parameters)
        {
            var command = connection.CreateCommand();

            command.CommandTimeout = outBoxTimeout < 0 ? 0 : outBoxTimeout;
            command.CommandText = sqlText;
            command.Parameters.AddRange(parameters);

            return command;
        }

        protected override IDbDataParameter[] CreatePagedOutstandingParameters(TimeSpan since, int pageSize,
            int pageNumber)
        {
            var parameters = new IDbDataParameter[3];
            parameters[0] = CreateSqlParameter("TimestampSince", DateTimeOffset.UtcNow.Subtract(since));
            parameters[1] = CreateSqlParameter("Take", pageSize);
            parameters[2] = CreateSqlParameter("Skip", Math.Max(pageNumber - 1, 0) * pageSize);

            return parameters;
        }

        protected override IDbDataParameter[] CreatePagedDispatchedParameters(TimeSpan dispatchedSince, int pageSize,
            int pageNumber)
        {
            var parameters = new IDbDataParameter[3];
            parameters[0] = CreateSqlParameter("DispatchedSince", DateTimeOffset.UtcNow.Subtract(dispatchedSince));
            parameters[1] = CreateSqlParameter("Take", pageSize);
            parameters[2] = CreateSqlParameter("Skip", Math.Max(pageNumber - 1, 0) * pageSize);

            return parameters;
        }

        protected override IDbDataParameter[] CreatePagedReadParameters(int pageSize, int pageNumber)
        {
            var parameters = new IDbDataParameter[2];
            parameters[0] = CreateSqlParameter("Take", pageSize);
            parameters[1] = CreateSqlParameter("Skip", Math.Max(pageNumber - 1, 0) * pageSize);

            return parameters;
        }

        protected override IDbDataParameter CreateSqlParameter(string parameterName, object? value)
        {
            return new NpgsqlParameter { ParameterName = parameterName, Value = value };
        }

        protected override IDbDataParameter[] InitAddDbParameters(Message message, int? position = null)
        {
            var prefix = position.HasValue ? $"p{position}_" : "";
            var bagjson = JsonSerializer.Serialize(message.Header.Bag, JsonSerialisationOptions.Options);
            return new[]
            {
                new NpgsqlParameter
                {
                    ParameterName = $"{prefix}MessageId", 
                    NpgsqlDbType = NpgsqlDbType.Text, 
                    Value = message.Id.Value
                },
                new NpgsqlParameter
                {
                    ParameterName = $"{prefix}MessageType",
                    NpgsqlDbType = NpgsqlDbType.Text,
                    Value = message.Header.MessageType.ToString()
                },
                new NpgsqlParameter
                {
                    ParameterName = $"{prefix}Topic",
                    NpgsqlDbType = NpgsqlDbType.Text,
                    Value = message.Header.Topic.Value
                },
                new NpgsqlParameter
                {
                    ParameterName = $"{prefix}Timestamp",
                    NpgsqlDbType = NpgsqlDbType.TimestampTz,
                    Value = message.Header.TimeStamp.ToUniversalTime()
                },
                new NpgsqlParameter
                {
                    ParameterName = $"{prefix}CorrelationId",
                    NpgsqlDbType = NpgsqlDbType.Text,
                    Value = message.Header.CorrelationId.Value
                },
                new NpgsqlParameter
                {
                    ParameterName = $"{prefix}ReplyTo",
                    NpgsqlDbType = NpgsqlDbType.Varchar,
                    Value = message.Header.ReplyTo is not null ? message.Header.ReplyTo.Value : DBNull.Value
                },
                new NpgsqlParameter
                {
                    ParameterName = $"{prefix}ContentType",
                    NpgsqlDbType = NpgsqlDbType.Varchar,
                    Value = message.Header.ContentType is not null ? message.Header.ContentType.ToString() : new ContentType(MediaTypeNames.Text.Plain).ToString()
                },
                new NpgsqlParameter
                {
                    ParameterName = $"{prefix}PartitionKey",
                    NpgsqlDbType = NpgsqlDbType.Varchar,
                    Value = message.Header.PartitionKey.Value
                },
                new NpgsqlParameter
                {
                    ParameterName = $"{prefix}HeaderBag", 
                    NpgsqlDbType = NpgsqlDbType.Text, 
                    Value = bagjson
                },
                new NpgsqlParameter
                {
                    ParameterName = $"{prefix}Source",
                    DbType = DbType.String,
                    Value = message.Header.Source.AbsoluteUri
                },
                new NpgsqlParameter
                {
                    ParameterName = $"{prefix}Type",
                    DbType = DbType.String,
                    Value = message.Header.Type
                },
                new NpgsqlParameter
                {
                    ParameterName = $"{prefix}DataSchema",
                    DbType = DbType.String,
                    Value = message.Header.DataSchema is not null ? message.Header.DataSchema.AbsoluteUri : DBNull.Value
                },
                new NpgsqlParameter
                {
                    ParameterName = $"{prefix}Subject",
                    DbType = DbType.String,
                    Value = message.Header.Subject is not null ? message.Header.Subject : DBNull.Value
                },
                new NpgsqlParameter
                {
                    ParameterName = $"{prefix}SpecVersion",
                    DbType = DbType.String,
                    Value = message.Header.SpecVersion
                },
                new NpgsqlParameter
                {
                    ParameterName = $"{prefix}TraceParent",
                    DbType = DbType.String,
                    Value = message.Header.TraceParent is not null ? message.Header.TraceParent.Value : DBNull.Value
                },
                new NpgsqlParameter
                {
                    ParameterName = $"{prefix}TraceState",
                    DbType = DbType.String,
                    Value = message.Header.TraceState is not null ? message.Header.TraceState.Value : DBNull.Value
                },
                new NpgsqlParameter
                {
                    ParameterName = $"{prefix}Baggage",
                    DbType = DbType.String,
                    Value = message.Header.Baggage.ToString()
                },
                _configuration.BinaryMessagePayload
                    ? new NpgsqlParameter
                    {
                        ParameterName = $"{prefix}Body",
                        NpgsqlDbType = NpgsqlDbType.Bytea,
                        Value = message.Body.Bytes
                    }
                    : new NpgsqlParameter
                    {
                        ParameterName = $"{prefix}Body",
                        NpgsqlDbType = NpgsqlDbType.Text,
                        Value = message.Body.Value
                    }
            };
        }

        protected override Message MapFunction(DbDataReader dr)
        {
            if (dr.Read())
            {
                return MapAMessage(dr);
            }

            return new Message();
        }

        protected override async Task<Message> MapFunctionAsync(DbDataReader dr, CancellationToken cancellationToken)
        {
            if (await dr.ReadAsync(cancellationToken))
            {
                return MapAMessage(dr);
            }

            return new Message();
        }

        protected override IEnumerable<Message> MapListFunction(DbDataReader dr)
        {
            var messages = new List<Message>();
            while (dr.Read())
            {
                messages.Add(MapAMessage(dr));
            }

            dr.Close();

            return messages;
        }

        protected override async Task<IEnumerable<Message>> MapListFunctionAsync(
            DbDataReader dr,
            CancellationToken cancellationToken
        )
        {
            var messages = new List<Message>();
            while (await dr.ReadAsync(cancellationToken))
            {
                messages.Add(MapAMessage(dr));
            }

            dr.Close();

            return messages;
        }

        protected override async Task<int> MapOutstandingCountAsync(DbDataReader dr,
            CancellationToken cancellationToken)
        {
            int outstandingMessages = -1;
            if (await dr.ReadAsync(cancellationToken))
            {
                outstandingMessages = dr.GetInt32(0);
            }

            await dr.CloseAsync();

            return outstandingMessages;
        }

        protected override int MapOutstandingCount(DbDataReader dr)
        {
            int outstandingMessages = -1;
            if (dr.Read())
            {
                outstandingMessages = dr.GetInt32(0);
            }

            dr.Close();

            return outstandingMessages;
        }

        private Message MapAMessage(DbDataReader dr)
        {
            var id = GetMessageId(dr);
            var messageType = GetMessageType(dr);
            var topic = GetTopic(dr);

            DateTimeOffset timeStamp = GetTimeStamp(dr);
            var correlationId = GetCorrelationId(dr);
            var replyTo = GetReplyTo(dr);
            var contentType = GetContentType(dr);
            var partitionKey = GetPartitionKey(dr);

            var source = GetSource(dr);
            var type = GetEventType(dr);
            var dataSchema = GetDataSchema(dr);
            var subject = GetSubject(dr);
            var specVersion = GetSpecVersion(dr);
            var traceParent = GetTraceParent(dr);
            var traceState = GetTraceState(dr);
            var baggage = GetBaggage(dr);
            var dataRef = GetDataRef(dr);

            var header = new MessageHeader(
                messageId: new Id(id),
                topic: topic,
                messageType: messageType,
                source: source,
                type: type,
                timeStamp: timeStamp,
                correlationId: correlationId,
                replyTo: replyTo ,
                contentType: contentType,
                partitionKey: partitionKey,
                dataSchema: dataSchema,
                subject: subject,
                handledCount: 0, // HandledCount is zero when restored from the Outbox
                delayed: TimeSpan.Zero, // Delayed is zero when restored from the Outbox
                traceParent: traceParent,
                traceState: traceState,
                baggage: baggage
            );
            header.SpecVersion = specVersion ?? MessageHeader.DefaultSpecVersion;
            header.DataRef = dataRef;
            Dictionary<string, object>? dictionaryBag = GetContextBag(dr);
            if (dictionaryBag != null)
            {
                foreach (var keyValue in dictionaryBag)
                {
                    header.Bag.Add(keyValue.Key, keyValue.Value);
                }
            }

            var body = _configuration.BinaryMessagePayload
                ? new MessageBody(((NpgsqlDataReader)dr).GetFieldValue<byte[]>(dr.GetOrdinal("Body")))
                : new MessageBody(dr.GetString(dr.GetOrdinal("Body")));

            return new Message(header, body);
        }
        
        private static Baggage GetBaggage (DbDataReader dr)
        {
            var baggage = new Baggage();
            var (ordinal, err) = TryGetOrdinal(dr, "Baggage");
            if (err || dr.IsDBNull(ordinal)) return baggage;

            var baggageString = dr.GetString(ordinal);
            if (string.IsNullOrEmpty(baggageString))
                return baggage;
           
            baggage.LoadBaggage(baggageString);
            return baggage;
        }

        private static ContentType GetContentType(DbDataReader dr)
        {
            var (ordinal, err) = TryGetOrdinal(dr, "ContentType");
            if (err || dr.IsDBNull(ordinal)) return new ContentType(MediaTypeNames.Text.Plain);
            
            var replyTo = dr.GetString(ordinal);
            if (string.IsNullOrEmpty(replyTo))
                return new ContentType(MediaTypeNames.Text.Plain);
            
            return new ContentType(replyTo);
        }

        private static Dictionary<string, object>? GetContextBag(DbDataReader dr)
        {
            var (ordinal, err) = TryGetOrdinal(dr, "HeaderBag");
            if (err || dr.IsDBNull(ordinal)) return new Dictionary<string, object>();

            var headerBag = dr.GetString(ordinal);
            var dictionaryBag = JsonSerializer.Deserialize<Dictionary<string, object>>(headerBag, JsonSerialisationOptions.Options);
            return dictionaryBag;
        }

        private static Id GetCorrelationId(DbDataReader dr)
        {
            var (ordinal, err) = TryGetOrdinal(dr, "CorrelationId");
            if (err || dr.IsDBNull(ordinal)) return Id.Empty;
            
            var correlationId = dr.GetString(ordinal);
            if (string.IsNullOrEmpty(correlationId))
                return Id.Empty;
            
            return new Id(correlationId);
        }
        
        private static string GetDataRef(DbDataReader dr)
        {
            var (ordinal, err) = TryGetOrdinal(dr, "DataRef");
            if (err || dr.IsDBNull(ordinal)) return string.Empty;
            
            var dataRef = dr.GetString(ordinal);
            return string.IsNullOrEmpty(dataRef) ? string.Empty : dataRef;
        }
        
        private static Uri? GetDataSchema(DbDataReader dr)
        {
            var (ordinal, err) = TryGetOrdinal(dr, "DataSchema");
            if (err || dr.IsDBNull(ordinal)) return null;
            
            var dataSchema = dr.GetString(ordinal);
            return string.IsNullOrEmpty(dataSchema) ? null : new Uri(dataSchema);
        }
        
        private static CloudEventsType GetEventType(DbDataReader dr)
        {
            var (ordinal, err) = TryGetOrdinal(dr, "Type");
            if (err || dr.IsDBNull(ordinal)) return CloudEventsType.Empty;
            
            var type = dr.GetString(ordinal);
            if (string.IsNullOrEmpty(type))
                return CloudEventsType.Empty;
            
            return new CloudEventsType(type);
        }

        private static RoutingKey GetTopic(DbDataReader dr)
        {
            var (ordinal, err) = TryGetOrdinal(dr, "Topic");
            if (err || dr.IsDBNull(ordinal)) return RoutingKey.Empty;
           
           var topic = dr.GetString(ordinal);
            if (string.IsNullOrEmpty(topic))
                return RoutingKey.Empty;
            
            return new RoutingKey(topic);
        }

        private static MessageType GetMessageType(DbDataReader dr)
        {
            var (ordinal, err) = TryGetOrdinal(dr, "MessageType");
            if (err || dr.IsDBNull(ordinal)) return MessageType.MT_NONE;

            var value = dr.GetString(ordinal);
            if (string.IsNullOrEmpty(value))
                return MessageType.MT_NONE;
            
            return (MessageType)Enum.Parse(typeof(MessageType), value);
        }

        private static Id GetMessageId(DbDataReader dr)
        {
            var (ordinal, err) = TryGetOrdinal(dr, "MessageId");
            if (err || dr.IsDBNull(ordinal)) return Id.Random();
 
            var id = dr.GetString(ordinal);
            return new Id(id);
        }

        private static PartitionKey GetPartitionKey(DbDataReader dr)
        {
            var (ordinal, err) = TryGetOrdinal(dr, "PartitionKey");
            if (err || dr.IsDBNull(ordinal)) return PartitionKey.Empty;


            var partitionKey = dr.GetString(ordinal);
            return new PartitionKey(partitionKey);
        }

        private static RoutingKey GetReplyTo(DbDataReader dr)
        {
            var (ordinal, err) = TryGetOrdinal(dr, "ReplyTo");
            if (err || dr.IsDBNull(ordinal)) return RoutingKey.Empty;

            var replyTo = dr.GetString(ordinal);
            if (string.IsNullOrEmpty(replyTo))
                return RoutingKey.Empty;
            
            return new RoutingKey(replyTo);
        }
        
        private static Uri? GetSource(DbDataReader dr)
        {
            var (ordinal, err) = TryGetOrdinal(dr, "Source");
            if (err || dr.IsDBNull(ordinal)) return null;
            
            var source = dr.GetString(ordinal);
            return string.IsNullOrEmpty(source) ? null : new Uri(source);
        }
        
        private static string GetSpecVersion(DbDataReader dr)
        {
            var (ordinal, err) = TryGetOrdinal(dr, "SpecVersion");
            if (err || dr.IsDBNull(ordinal)) return MessageHeader.DefaultSpecVersion;
            
            var specVersion = dr.GetString(ordinal);
            return string.IsNullOrEmpty(specVersion) ? MessageHeader.DefaultSpecVersion : specVersion;
        }
        
        private static string GetSubject(DbDataReader dr)
        {
            var (ordinal, err) = TryGetOrdinal(dr, "Subject");
            if (err || dr.IsDBNull(ordinal)) return string.Empty;
            
            var subject = dr.GetString(ordinal);
            return string.IsNullOrEmpty(subject) ? string.Empty : subject;
        }

        private static DateTimeOffset GetTimeStamp(DbDataReader dr)
        {
            var (ordinal, err) = TryGetOrdinal(dr, "Timestamp");
            if (err || dr.IsDBNull(ordinal)) return DateTimeOffset.UtcNow;
            
            var timeStamp = dr.IsDBNull(ordinal)
                ? DateTimeOffset.MinValue
                : dr.GetDateTime(ordinal);
            return timeStamp;
        }
        
        private static string GetTraceParent(DbDataReader dr)
        {
            var (ordinal, err) = TryGetOrdinal(dr, "TraceParent");
            if (err || dr.IsDBNull(ordinal)) return string.Empty;
            
            var traceParent = dr.GetString(ordinal);
            return string.IsNullOrEmpty(traceParent) ? string.Empty : traceParent;
        }
        
        private static string GetTraceState(DbDataReader dr)
        {
            var (ordinal, err) = TryGetOrdinal(dr, "TraceState");
            if (err || dr.IsDBNull(ordinal)) return string.Empty;
            
            var traceState = dr.GetString(ordinal);
            return string.IsNullOrEmpty(traceState) ? string.Empty : traceState;
        }
        
        private static (int, bool) TryGetOrdinal(DbDataReader dr, string columnName)
        {
            try
            {
                return (dr.GetOrdinal(columnName), false);
            }
            catch (IndexOutOfRangeException)
            {
                // SpecVersion column does not exist, return -1 and true to indicate error
                return (-1, true);
            }
        }

        private static partial class Log
        {
            [LoggerMessage(LogLevel.Warning, "PostgresSqlOutbox: A duplicate was detected in the batch")]
            public static partial void DuplicateDetectedInBatch(ILogger logger);
        }
    }
}
