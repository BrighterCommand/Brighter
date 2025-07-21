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
using System.IO;
using System.Net.Mime;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Logging;
using Paramore.Brighter.MySql;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Outbox.MySql
{
    /// <summary>
    /// Implements an outbox using Sqlite as a backing store  
    /// </summary>
    public partial class MySqlOutbox : RelationDatabaseOutbox
    {
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<MySqlOutbox>();

        private const int MySqlDuplicateKeyError = 1062;
        private readonly IAmARelationalDatabaseConfiguration _configuration;
        private readonly IAmARelationalDbConnectionProvider _connectionProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="MySqlOutbox" /> class.
        /// </summary>
        /// <param name="configuration">The configuration to connect to this data store</param>
        /// <param name="connectionProvider">Provides a connection to the Db that allows us to enlist in an ambient transaction</param>
        public MySqlOutbox(IAmARelationalDatabaseConfiguration configuration,
            IAmARelationalDbConnectionProvider connectionProvider)
            : base(DbSystem.MySql, configuration.DatabaseName, configuration.OutBoxTableName,
                new MySqlQueries(), ApplicationLogging.CreateLogger<MySqlOutbox>())
        {
            _configuration = configuration;
            _connectionProvider = connectionProvider;
            ContinueOnCapturedContext = false;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MySqlOutbox" /> class.
        /// </summary>
        /// <param name="configuration">The configuration to connect to this data store</param>
        public MySqlOutbox(IAmARelationalDatabaseConfiguration configuration)
            : this(configuration, new MySqlConnectionProvider(configuration))
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
            catch (MySqlException sqlException)
            {
                if (!IsExceptionUnqiueOrDuplicateIssue(sqlException)) throw;
                Log.DuplicateDetectedInBatch(s_logger);
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
            CancellationToken cancellationToken
        )
        {
            var connection = await GetOpenConnectionAsync(_connectionProvider, transactionProvider, cancellationToken);
            using var command = commandFunc.Invoke(connection);
            try
            {
                if (transactionProvider is { HasOpenTransaction: true })
                    command.Transaction = await transactionProvider.GetTransactionAsync(cancellationToken);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (MySqlException sqlException)
            {
                if (IsExceptionUnqiueOrDuplicateIssue(sqlException))
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

        protected override IDbDataParameter[] CreatePagedDispatchedParameters(TimeSpan dispatchedSince, int pageSize,
            int pageNumber)
        {
            var parameters = new IDbDataParameter[3];
            parameters[0] = CreateSqlParameter("Skip", Math.Max(pageNumber - 1, 0) * pageSize);
            parameters[1] = CreateSqlParameter("Take", pageSize);
            parameters[2] = CreateSqlParameter("DispatchedSince", DateTimeOffset.UtcNow.Subtract(dispatchedSince));

            return parameters;
        }

        protected override IDbDataParameter[] CreatePagedReadParameters(int pageSize, int pageNumber)
        {
            var parameters = new IDbDataParameter[2];
            parameters[0] = CreateSqlParameter("Skip", Math.Max(pageNumber - 1, 0) * pageSize);
            parameters[1] = CreateSqlParameter("Take", pageSize);

            return parameters;
        }

        protected override IDbDataParameter CreateSqlParameter(string parameterName, object? value)
        {
            return new MySqlParameter { ParameterName = parameterName, Value = value };
        }


        protected override IDbDataParameter[] InitAddDbParameters(Message message, int? position = null)
        {
            var prefix = position.HasValue ? $"p{position}_" : "";
            var bagJson = JsonSerializer.Serialize(message.Header.Bag, JsonSerialisationOptions.Options);

            return new[]
            {
                new MySqlParameter { ParameterName = $"@{prefix}MessageId", DbType = DbType.String, Value = message.Id.Value },
                new MySqlParameter { ParameterName = $"@{prefix}MessageType", DbType = DbType.String, Value = message.Header.MessageType.ToString() },
                new MySqlParameter { ParameterName = $"@{prefix}Topic", DbType = DbType.String, Value = message.Header.Topic.Value, },
                new MySqlParameter
                {
                    ParameterName = $"@{prefix}Timestamp", DbType = DbType.DateTimeOffset, Value = message.Header.TimeStamp.ToUniversalTime()
                }, //always store in UTC, as this is how we query messages
                new MySqlParameter { ParameterName = $"@{prefix}CorrelationId", DbType = DbType.String, Value = message.Header.CorrelationId.Value },
                new MySqlParameter { ParameterName = $"@{prefix}ReplyTo", DbType = DbType.String, Value = message.Header.ReplyTo?.Value },
                new MySqlParameter { ParameterName = $"@{prefix}ContentType", DbType = DbType.String, Value = message.Header.ContentType?.ToString() },
                new MySqlParameter { ParameterName = $"@{prefix}PartitionKey", DbType = DbType.String, Value = message.Header.PartitionKey.Value },
                new MySqlParameter { ParameterName = $"@{prefix}HeaderBag", DbType = DbType.String, Value = bagJson }, _configuration.BinaryMessagePayload
                    ? new MySqlParameter { ParameterName = $"@{prefix}Body", DbType = DbType.Binary, Value = message.Body.Bytes }
                    : new MySqlParameter { ParameterName = $"@{prefix}Body", DbType = DbType.String, Value = message.Body.Value },
                new MySqlParameter { ParameterName = $"@{prefix}Source", DbType = DbType.String, Value = message.Header.Source.AbsoluteUri },
                new MySqlParameter { ParameterName = $"@{prefix}Type", DbType = DbType.String, Value = message.Header.Type },
                new MySqlParameter { ParameterName = $"@{prefix}DataSchema", DbType = DbType.String, Value = message.Header.DataSchema?.AbsoluteUri },
                new MySqlParameter { ParameterName = $"@{prefix}Subject", DbType = DbType.String, Value = message.Header.Subject },
                new MySqlParameter { ParameterName = $"@{prefix}TraceParent", DbType = DbType.String, Value = message.Header.TraceParent?.Value },
                new MySqlParameter { ParameterName = $"@{prefix}TraceState", DbType = DbType.String, Value = message.Header.TraceState?.Value },
                new MySqlParameter { ParameterName = $"@{prefix}Baggage", DbType = DbType.String, Value = message.Header.Baggage.ToString() }
            };
        }

        protected override IDbDataParameter[] CreatePagedOutstandingParameters(TimeSpan since, int pageSize,
            int pageNumber)
        {
            var parameters = new IDbDataParameter[3];
            parameters[0] = CreateSqlParameter("Skip", Math.Max(pageNumber - 1, 0) * pageSize);
            parameters[1] = CreateSqlParameter("Take", pageSize);
            parameters[2] = CreateSqlParameter("TimestampSince", DateTimeOffset.UtcNow.Subtract(since));

            return parameters;
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
            CancellationToken cancellationToken)
        {
            var messages = new List<Message>();
            while (await dr.ReadAsync(cancellationToken))
            {
                messages.Add(MapAMessage(dr));
            }
#if NETSTANDARD2_0
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

#if NETSTANDARD2_0
            dr.Close();
#else
            await dr.CloseAsync();
#endif

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

        private static bool IsExceptionUnqiueOrDuplicateIssue(MySqlException sqlException)
        {
            return sqlException.Number == MySqlDuplicateKeyError;
        }

        private Message MapAMessage(DbDataReader dr)
        {
            var id = GetMessageId(dr);
            var messageType = GetMessageType(dr);
            var topic = GetTopic(dr);

            var header = new MessageHeader(id, topic, messageType);

            if (dr.FieldCount > 4)
            {
                var timeStamp = GetTimeStamp(dr);
                var correlationId = GetCorrelationId(dr);
                var replyTo = GetReplyTo(dr);
                var contentType = GetContentType(dr) ?? new ContentType(MediaTypeNames.Text.Plain);
                var partitionKey = GetPartitionKey(dr);
                var source = GetSource(dr);
                var eventType = GetEventType(dr);
                var dataSchema = GetDataSchema(dr);
                var subject = GetSubject(dr);
                var traceParent = GetTraceParent(dr);
                var traceState = GetTraceState(dr);
                var baggage = GetBaggage(dr);
                
                header = new MessageHeader(
                    messageId: id,
                    topic: topic,
                    messageType: messageType,
                    source: source,
                    type: eventType,
                    timeStamp: timeStamp,
                    correlationId: correlationId,
                    replyTo: replyTo,
                    contentType: contentType,
                    partitionKey: partitionKey,
                    dataSchema: dataSchema,
                    subject: subject,
                    handledCount: 0,
                    delayed: TimeSpan.Zero,
                    traceParent: traceParent,
                    traceState: traceState,
                    baggage: baggage
                );

                // existing bag items
                var dictionaryBag = GetContextBag(dr);
                if (dictionaryBag != null)
                    foreach (var kv in dictionaryBag)
                        header.Bag.Add(kv.Key, kv.Value);
            }

#if NETSTANDARD2_0
            var body = _configuration.BinaryMessagePayload
                ? new MessageBody(GetBodyAsBytes((MySqlDataReader)dr), new ContentType(MediaTypeNames.Application.Octet), CharacterEncoding.Raw)
                : new MessageBody(GetBodyAsString(dr), new ContentType("application/json"), CharacterEncoding.UTF8);

#else
            var body = _configuration.BinaryMessagePayload
                ? new MessageBody(GetBodyAsBytes((MySqlDataReader)dr), new ContentType(MediaTypeNames.Application.Octet), CharacterEncoding.Raw)
                : new MessageBody(GetBodyAsString(dr), new ContentType(MediaTypeNames.Application.Json), CharacterEncoding.UTF8);

#endif

            return new Message(header, body);
        }

        private static byte[] GetBodyAsBytes(MySqlDataReader dr)
        {
            // No need to dispose a MemoryStream, I do not think they dare to ever change that
            var stream = dr.GetStream("Body");
            if (stream is not MemoryStream memoryStream) // the current implementation returns a MemoryStream
                // If the type of returned Stream is ever changed, please check if it requires disposal, also other places in the code base that uses GetStream
                throw new NotImplementedException(nameof(MySqlDataReader.GetStream) + " no longer returns " + nameof(MemoryStream));

            return memoryStream.ToArray(); // Then we can just return its value, instead of copying manually
        }

        private static string GetBodyAsString(IDataReader dr)
        {
            return dr.GetString(dr.GetOrdinal("Body"));
        }

        private static Dictionary<string, object> GetContextBag(IDataReader dr)
        {
            var i = dr.GetOrdinal("HeaderBag");
            var headerBag = dr.IsDBNull(i) ? "" : dr.GetString(i);
            return JsonSerializer.Deserialize<Dictionary<string, object>>(headerBag, JsonSerialisationOptions.Options)!;
        }

        private static ContentType? GetContentType(IDataReader dr)
        {
            var ordinal = dr.GetOrdinal("ContentType");
            if (dr.IsDBNull(ordinal)) return null;

            var contentType = dr.GetString(ordinal);
            if (string.IsNullOrEmpty(contentType))
                return null;
            return new ContentType(contentType);
        }

        private static Id? GetCorrelationId(IDataReader dr)
        {
            var ordinal = dr.GetOrdinal("CorrelationId");
            if (dr.IsDBNull(ordinal)) return null;

            var correlationId = dr.GetString(ordinal);
            return new Id(correlationId);
        }

        private static MessageType GetMessageType(IDataReader dr)
        {
            return (MessageType)Enum.Parse(typeof(MessageType), dr.GetString(dr.GetOrdinal("MessageType")));
        }

        private static Id GetMessageId(IDataReader dr)
        {
            var id = dr.GetString(dr.GetOrdinal("MessageId"));
            return new Id(id);
        }

        private static PartitionKey? GetPartitionKey(IDataReader dr)
        {
            var ordinal = dr.GetOrdinal("PartitionKey");
            if (dr.IsDBNull(ordinal)) return null;

            var partitionKey = dr.GetString(ordinal);
            return new PartitionKey(partitionKey);
        }

        private static RoutingKey? GetReplyTo(IDataReader dr)
        {
            var ordinal = dr.GetOrdinal("ReplyTo");
            if (dr.IsDBNull(ordinal)) return null;

            var replyTo = dr.GetString(ordinal);
            if (string.IsNullOrEmpty(replyTo))
                return null;
            return new RoutingKey(replyTo);
        }

        private static RoutingKey GetTopic(IDataReader dr)
        {
            return new RoutingKey(dr.GetString(dr.GetOrdinal("Topic")));
        }

        private static DateTimeOffset GetTimeStamp(IDataReader dr)
        {
            var ordinal = dr.GetOrdinal("Timestamp");
            var timeStamp = dr.IsDBNull(ordinal)
                ? DateTimeOffset.MinValue
                : dr.GetDateTime(ordinal);
            return timeStamp;
        }

        private static Uri? GetSource(IDataReader dr)
        {
            var ord = dr.GetOrdinal("Source");
            return dr.IsDBNull(ord) ? null : new Uri(dr.GetString(ord));
        }

        private static string? GetEventType(IDataReader dr)
        {
            var ord = dr.GetOrdinal("Type");
            return dr.IsDBNull(ord) ? null : dr.GetString(ord);
        }

        private static Uri? GetDataSchema(IDataReader dr)
        {
            var ord = dr.GetOrdinal("DataSchema");
            return dr.IsDBNull(ord) ? null : new Uri(dr.GetString(ord));
        }

        private static string? GetSubject(IDataReader dr)
        {
            var ord = dr.GetOrdinal("Subject");
            return dr.IsDBNull(ord) ? null : dr.GetString(ord);
        }

        private static TraceParent? GetTraceParent(IDataReader dr)
        {
            var ord = dr.GetOrdinal("TraceParent");
            return dr.IsDBNull(ord) ? null : new TraceParent(dr.GetString(ord));
        }

        private static TraceState? GetTraceState(IDataReader dr)
        {
            var ord = dr.GetOrdinal("TraceState");
            return dr.IsDBNull(ord) ? null : new TraceState(dr.GetString(ord));
        }

        private static Baggage GetBaggage(IDataReader dr)
        {
            var baggage = new Baggage();
            
            var ord = dr.GetOrdinal("Baggage");
            
            var baggageAsString = dr.IsDBNull(ord) ? "" : dr.GetString(ord);
            if (string.IsNullOrEmpty(baggageAsString))
                return baggage;
            
            baggage.LoadBaggage(baggageAsString);
            return baggage;
        }

        private static partial class Log
        {
            [LoggerMessage(LogLevel.Warning, "MsSqlOutbox: A duplicate was detected in the batch")]
            public static partial void DuplicateDetectedInBatch(ILogger logger);
        }
    }
}
