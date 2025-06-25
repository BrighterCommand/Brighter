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
using System.Globalization;
using System.Net.Mime;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Sqlite;

namespace Paramore.Brighter.Outbox.Sqlite
{
    /// <summary>
    /// Implements an outbox using Sqlite as a backing store
    /// </summary>
    public class SqliteOutbox : RelationDatabaseOutbox
    {
        private const int SqliteDuplicateKeyError = 1555;
        private const int SqliteUniqueKeyError = 19;
        private readonly IAmARelationalDatabaseConfiguration _configuration;
        private readonly IAmARelationalDbConnectionProvider _connectionProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqliteOutbox" /> class.
        /// </summary>
        /// <param name="configuration">The configuration to connect to this data store</param>
        /// <param name="connectionProvider">Provides a connection to the Db that allows us to enlist in an ambient transaction</param>
        public SqliteOutbox(IAmARelationalDatabaseConfiguration configuration,
            IAmARelationalDbConnectionProvider connectionProvider)
            : base(DbSystem.Sqlite, configuration.DatabaseName, configuration.OutBoxTableName, 
                  new SqliteQueries(), ApplicationLogging.CreateLogger<SqliteOutbox>())
        {
            _configuration = configuration;
            ContinueOnCapturedContext = false;
            _connectionProvider = connectionProvider;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqliteOutbox" /> class.
        /// </summary>
        /// <param name="configuration">The configuration to connect to this data store</param>
        public SqliteOutbox(IAmARelationalDatabaseConfiguration configuration)
            : this(configuration, new SqliteConnectionProvider(configuration))
        {
        }

        protected override void WriteToStore(
            IAmABoxTransactionProvider<DbTransaction>? transactionProvider,
            Func<DbConnection, DbCommand> commandFunc,
            Action? loggingAction
        )
        {
            var connection = GetOpenConnection(_connectionProvider, transactionProvider);
            using var command = commandFunc.Invoke(connection);
            try
            {
                if (transactionProvider is { HasOpenTransaction: true })
                    command.Transaction = transactionProvider.GetTransaction();
                command.ExecuteNonQuery();
            }
            catch (SqliteException sqlException)
            {
                if (!IsExceptionUnqiueOrDuplicateIssue(sqlException)) throw;
                loggingAction?.Invoke();
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
            
#if NETSTANDARD
            using var command = commandFunc.Invoke(connection);
#else
            await using var command = commandFunc.Invoke(connection);
#endif
            try
            {
                if (transactionProvider != null && transactionProvider.HasOpenTransaction)
                    command.Transaction = await transactionProvider.GetTransactionAsync(cancellationToken);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (SqliteException sqlException)
            {
                if (!IsExceptionUnqiueOrDuplicateIssue(sqlException)) throw;
                loggingAction?.Invoke();
            }
            finally
            {
                if (transactionProvider != null)
                    transactionProvider.Close();
                else
#if NETSTANDARD2_0
                        connection.Close();
#else
                    await connection.CloseAsync();
#endif
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
            CancellationToken cancellationToken)
        {
            var connection = await _connectionProvider.GetConnectionAsync(cancellationToken);

            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync(cancellationToken);
#if NETSTANDARD2_0
            using var command = commandFunc.Invoke(connection);
#else
            await using var command = commandFunc.Invoke(connection);
#endif
            try
            {
                return await resultFunc.Invoke(await command.ExecuteReaderAsync(cancellationToken));
            }
            finally
            {
#if NETSTANDARD2_0
                        connection.Close();
#else
                await connection.CloseAsync();
#endif
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
            return new SqliteParameter(parameterName, value ?? DBNull.Value);
        }

        protected override IDbDataParameter[] InitAddDbParameters(Message message, int? position = null)
        {
            var prefix = position.HasValue ? $"p{position}_" : "";
            var bagJson = JsonSerializer.Serialize(message.Header.Bag, JsonSerialisationOptions.Options);
            return new IDbDataParameter[]
            {
                new SqliteParameter
                {
                    ParameterName = $"@{prefix}MessageId", SqliteType = SqliteType.Text, Value = message.Id.Value
                },
                new SqliteParameter
                {
                    ParameterName = $"@{prefix}MessageType",
                    SqliteType = SqliteType.Text,
                    Value = message.Header.MessageType.ToString()
                },
                new SqliteParameter
                {
                    ParameterName = $"@{prefix}Topic",
                    SqliteType = SqliteType.Text,
                    Value = message.Header.Topic.Value
                },
                new SqliteParameter
                {
                    ParameterName = $"@{prefix}Timestamp",
                    SqliteType = SqliteType.Text,
                    Value = message.Header.TimeStamp.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)
                },
                new SqliteParameter
                {
                    ParameterName = $"@{prefix}CorrelationId",
                    SqliteType = SqliteType.Text,
                    Value = message.Header.CorrelationId.Value
                },
                new SqliteParameter
                {
                    ParameterName = $"@{prefix}ReplyTo",
                    SqliteType = SqliteType.Text,
                    Value = message.Header.ReplyTo is not null ? message.Header.ReplyTo.Value : RoutingKey.Empty.Value
                },
                new SqliteParameter
                {
                    ParameterName = $"@{prefix}ContentType",
                    SqliteType = SqliteType.Text,
                    Value = message.Header.ContentType != null ? message.Header.ContentType.ToString() : new ContentType(MediaTypeNames.Text.Plain).ToString()
                },
                new SqliteParameter
                {
                    ParameterName = $"@{prefix}PartitionKey",
                    SqliteType = SqliteType.Text,
                    Value = message.Header.PartitionKey.Value
                },
                new SqliteParameter
                {
                    ParameterName = $"@{prefix}HeaderBag", SqliteType = SqliteType.Text, Value = bagJson
                },
                _configuration.BinaryMessagePayload
                    ? new SqliteParameter
                    {
                        ParameterName = $"@{prefix}Body", SqliteType = SqliteType.Blob, Value = message.Body.Bytes
                    }
                    : new SqliteParameter
                    {
                        ParameterName = $"@{prefix}Body", SqliteType = SqliteType.Text, Value = message.Body.Value
                    },
                new SqliteParameter
                {
                    ParameterName = $"@{prefix}Source",
                    SqliteType = SqliteType.Text,
                    Value = message.Header.Source.AbsoluteUri
                },
                new SqliteParameter
                {
                    ParameterName = $"@{prefix}Type",
                    SqliteType = SqliteType.Text,
                    Value = message.Header.Type
                },
                new SqliteParameter
                {
                    ParameterName = $"@{prefix}DataSchema",
                    SqliteType = SqliteType.Text,
                    Value = message.Header.DataSchema is not null ? message.Header.DataSchema.AbsoluteUri : "http://goparamore.io"
                },
               new SqliteParameter
               {
                    ParameterName = $"@{prefix}Subject",
                    SqliteType = SqliteType.Text,
                    Value = message.Header.Subject ?? string.Empty
                },
                new SqliteParameter
                {
                    ParameterName = $"@{prefix}TraceParent",
                    SqliteType = SqliteType.Text,
                    Value = message.Header.TraceParent is not null ? message.Header.TraceParent.Value : DBNull.Value
                },
                new SqliteParameter
                {
                    ParameterName = $"@{prefix}TraceState",
                    SqliteType = SqliteType.Text,
                    Value = message.Header.TraceState is not null ? message.Header.TraceState.Value  : DBNull.Value
                },
                new SqliteParameter
                {
                    ParameterName = $"@{prefix}Baggage",
                    SqliteType = SqliteType.Text,
                    Value = message.Header.Baggage.ToString()
               } 
            };
        }

        protected override Message MapFunction(DbDataReader dr)
        {
            using (dr)
            {
                if (dr.Read())
                {
                    return MapAMessage(dr);
                }

                return new Message();
            }
        }

        protected override async Task<Message> MapFunctionAsync(DbDataReader dr, CancellationToken cancellationToken)
        {
            using (dr)
            {
                if (await dr.ReadAsync(cancellationToken))
                {
                    return MapAMessage(dr);
                }

                return new Message();
            }
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

        protected override async Task<IEnumerable<Message>> MapListFunctionAsync(DbDataReader dr,
            CancellationToken cancellationToken)
        {
            var messages = new List<Message>();
            while (await dr.ReadAsync(cancellationToken))
            {
                messages.Add(MapAMessage(dr));
            }

#if NETSTANDARD
            dr.Close();
#else
            await dr.CloseAsync();
#endif 

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

#if NETSTANDARD
            dr.Close();
#else
            await dr.CloseAsync();
#endif 
            return outstandingMessages;
        }

        protected override int MapOutstandingCount(DbDataReader dr)
        {
            int outstandingMessages = -1;
            if ( dr.Read())
            {
                outstandingMessages = dr.GetInt32(0);
            }

            dr.Close();
            return outstandingMessages;
        }

        private static bool IsExceptionUnqiueOrDuplicateIssue(SqliteException sqlException)
        {
            return sqlException.SqliteErrorCode == SqliteDuplicateKeyError ||
                   sqlException.SqliteErrorCode == SqliteUniqueKeyError;
        }

        private Message MapAMessage(IDataReader dr)
        {
            var id = GetMessageId(dr);
            var messageType = GetMessageType(dr);
            var topic = GetTopic(dr);

            var header = new MessageHeader(id, topic, messageType);

            if (dr.FieldCount > 4)
            {
                DateTimeOffset timeStamp = GetTimeStamp(dr);
                var correlationId = GetCorrelationId(dr);
                var replyTo = GetReplyTo(dr);
                var contentType = GetContentType(dr);
                var partitionKey = GetPartitionKey(dr);
                var source = GetSource(dr);
                var type = GetType(dr);
                var dataSchema = GetDataSchema(dr);
                var subject = GetSubject(dr);
                var traceParent = GetTraceParent(dr);
                var traceState = GetTraceState(dr);
                var baggage = GetBaggage(dr);

                header = new MessageHeader(
                    messageId: id,
                    topic: topic,
                    messageType: messageType,
                    timeStamp: timeStamp,
                    handledCount: 0,
                    delayed: TimeSpan.Zero,
                    correlationId: correlationId is not null ? new Id(correlationId) : Id.Empty,
                    replyTo: replyTo is not null ? new RoutingKey(replyTo) : RoutingKey.Empty,
                    contentType: contentType,
                    partitionKey: partitionKey is not null ? new PartitionKey(partitionKey) : PartitionKey.Empty,
                   source: source,
                    type: type,
                    dataSchema: dataSchema,
                    subject: subject,
                    traceParent: traceParent,
                    traceState: traceState,
                    baggage: baggage
                    );

                Dictionary<string, object>? dictionaryBag = GetContextBag(dr);
                if (dictionaryBag != null)
                {
                    foreach (var keyValue in dictionaryBag)
                    {
                        header.Bag.Add(keyValue.Key, keyValue.Value);
                    }
                }
            }

            var body = _configuration.BinaryMessagePayload
                ? new MessageBody(GetBodyAsBytes((SqliteDataReader)dr), new ContentType(MediaTypeNames.Application.Octet), CharacterEncoding.Raw)
                : new MessageBody(dr.GetString(dr.GetOrdinal("Body")));


            return new Message(header, body);
        }

        private static Baggage GetBaggage(IDataReader dr)
        {
            var ordinal = dr.GetOrdinal("Baggage");
            if (ordinal < 0 || ordinal >= dr.FieldCount)
                return Baggage.Empty;
            
            return dr.IsDBNull(ordinal)
                ? Baggage.Empty
                : Baggage.FromString(dr.GetString(ordinal));
        }

        private static byte[] GetBodyAsBytes(SqliteDataReader dr)
        {
            var i = dr.GetOrdinal("Body");
            var body = dr.GetStream(i);
            
            if (body is MemoryStream memoryStream) // No need to dispose a MemoryStream, I do not think they dare to ever change that
                return memoryStream.ToArray(); // Then we can just return its value, instead of copying manually

            MemoryStream ms = new();
            body.CopyTo(ms);
            body.Dispose();
            return ms.ToArray();
        }

        private static Dictionary<string, object>? GetContextBag(IDataReader dr)
        {
            var i = dr.GetOrdinal("HeaderBag");
            var headerBag = dr.IsDBNull(i) ? "" : dr.GetString(i);
            var dictionaryBag =
                JsonSerializer.Deserialize<Dictionary<string, object>>(headerBag, JsonSerialisationOptions.Options);
            return dictionaryBag;
        }

        private ContentType? GetContentType(IDataReader dr)
        {
            var ordinal = dr.GetOrdinal("ContentType");
            if (dr.IsDBNull(ordinal)) return null;

            var contentType = dr.GetString(ordinal);
            return new ContentType(contentType);
        }

        private static string? GetCorrelationId(IDataReader dr)
        {
            var ordinal = dr.GetOrdinal("CorrelationId");
            if (dr.IsDBNull(ordinal)) return null;

            var correlationId = dr.GetString(ordinal);
            return correlationId;
        }
        
        private static Uri GetDataSchema(IDataReader dr)
        {
            var ordinal = dr.GetOrdinal("DataSchema");
            if (dr.IsDBNull(ordinal)) return new Uri("http://goparamore.io");

            var uriString = dr.GetString(ordinal);
            if (string.IsNullOrEmpty(uriString))
                return new Uri("http://goparamore.io");
            
            return new Uri(uriString);
        }

        private static MessageType GetMessageType(IDataReader dr)
        {
            var ordinal = dr.GetOrdinal("MessageType");
            if (dr.IsDBNull(ordinal)) return MessageType.MT_NONE;


            var value = dr.GetString(ordinal);
            if (string.IsNullOrEmpty(value))
                return MessageType.MT_NONE;
            
            return (MessageType)Enum.Parse(typeof(MessageType), value);
        }

        private static Id GetMessageId(IDataReader dr)
        {
            var ordinal = dr.GetOrdinal("MessageId");
            if (dr.IsDBNull(ordinal)) return Id.Empty;
            
            var id = dr.GetString(ordinal);
            if (string.IsNullOrEmpty(id))
                return Id.Empty;
            return new Id(id);
        }

        private static string? GetPartitionKey(IDataReader dr)
        {
            var ordinal = dr.GetOrdinal("PartitionKey");
            if (dr.IsDBNull(ordinal)) return null;

            var partitionKey = dr.GetString(ordinal);
            return partitionKey;
        }


        private static string? GetReplyTo(IDataReader dr)
        {
            var ordinal = dr.GetOrdinal("ReplyTo");
            if (dr.IsDBNull(ordinal)) return null;

            var replyTo = dr.GetString(ordinal);
            return replyTo;
        }

        private static Uri GetSource(IDataReader dr)
        {
            var ordinal = dr.GetOrdinal("Source");
            if (dr.IsDBNull(ordinal)) return new Uri("http://goparamore.io");

            var uriString = dr.GetString(ordinal);
            if (string.IsNullOrEmpty(uriString))
                return new Uri("http://goparamore.io");
            
            return new Uri(uriString);
        }
        
        private static string GetSubject(IDataReader dr)
        {
            var ordinal = dr.GetOrdinal("Subject");
            if (dr.IsDBNull(ordinal)) return string.Empty;
            
            return dr.GetString(ordinal);
        }

        private static RoutingKey GetTopic(IDataReader dr)
        {
            var ordinal = dr.GetOrdinal("Topic");
            if (dr.IsDBNull(ordinal)) return RoutingKey.Empty;


            var routingKey = dr.GetString(ordinal);
            if (string.IsNullOrEmpty(routingKey))
                return RoutingKey.Empty;
            
            return new RoutingKey(routingKey);
        }


        private static DateTimeOffset GetTimeStamp(IDataReader dr)
        {
            var ordinal = dr.GetOrdinal("Timestamp");
            var timeStamp = dr.IsDBNull(ordinal)
                ? DateTimeOffset.MinValue
                : dr.GetDateTime(ordinal);
            return timeStamp;
        }
        
        private static TraceParent? GetTraceParent(IDataReader dr)
        {
            var ordinal = dr.GetOrdinal("TraceParent");
            if (dr.IsDBNull(ordinal)) return null;
            
            return dr.IsDBNull(ordinal)
                ? null
                : new TraceParent(dr.GetString(ordinal));
        }

        private static TraceState? GetTraceState(IDataReader dr)
        {
            var ordinal = dr.GetOrdinal("TraceState");
            
            return dr.IsDBNull(ordinal)
                ? null
                : new TraceState(dr.GetString(ordinal));
        }
        
        private static string GetType(IDataReader dr)
        {
            var ordinal = dr.GetOrdinal("Type");
            if (dr.IsDBNull(ordinal)) return string.Empty;


            var type = dr.GetString(ordinal);
            if (string.IsNullOrEmpty(type))
                return string.Empty;
            return type;
        }


    }
}
