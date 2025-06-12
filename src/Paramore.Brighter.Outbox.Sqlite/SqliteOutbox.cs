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
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
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
            IAmABoxTransactionProvider<DbTransaction> transactionProvider,
            Func<DbConnection, DbCommand> commandFunc,
            Action loggingAction
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
                loggingAction.Invoke();
            }
            finally
            {
                FinishWrite(connection, transactionProvider);
            }
        }

        protected override async Task WriteToStoreAsync(
            IAmABoxTransactionProvider<DbTransaction> transactionProvider,
            Func<DbConnection, DbCommand> commandFunc,
            Action loggingAction,
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
                loggingAction.Invoke();
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

        protected override IDbDataParameter CreateSqlParameter(string parameterName, object value)
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
                    ParameterName = $"@{prefix}MessageId", SqliteType = SqliteType.Text, Value = message.Id
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
                    Value =
                        message.Header.TimeStamp.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)
                },
                new SqliteParameter
                {
                    ParameterName = $"@{prefix}CorrelationId",
                    SqliteType = SqliteType.Text,
                    Value = message.Header.CorrelationId
                },
                new SqliteParameter
                {
                    ParameterName = $"@{prefix}ReplyTo",
                    SqliteType = SqliteType.Text,
                    Value = message.Header.ReplyTo
                },
                new SqliteParameter
                {
                    ParameterName = $"@{prefix}ContentType",
                    SqliteType = SqliteType.Text,
                    Value = message.Header.ContentType
                },
                new SqliteParameter
                {
                    ParameterName = $"@{prefix}PartitionKey",
                    SqliteType = SqliteType.Text,
                    Value = message.Header.PartitionKey
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

                header = new MessageHeader(
                    messageId: id,
                    topic: topic,
                    messageType: messageType,
                    timeStamp: timeStamp,
                    handledCount: 0,
                    delayed: TimeSpan.Zero,
                    correlationId: correlationId,
                    replyTo: new RoutingKey(replyTo),
                    contentType: contentType,
                    partitionKey: partitionKey);

                Dictionary<string, object> dictionaryBag = GetContextBag(dr);
                if (dictionaryBag != null)
                {
                    foreach (var keyValue in dictionaryBag)
                    {
                        header.Bag.Add(keyValue.Key, keyValue.Value);
                    }
                }
            }

            var body = _configuration.BinaryMessagePayload
                ? new MessageBody(GetBodyAsBytes((SqliteDataReader)dr), "application/octet-stream",
                    CharacterEncoding.Raw)
                : new MessageBody(dr.GetString(dr.GetOrdinal("Body")));


            return new Message(header, body);
        }


        private static byte[] GetBodyAsBytes(DbDataReader dr)
        {
            var i = dr.GetOrdinal("Body");
            using var body = dr.GetStream(i);
            if (body is MemoryStream memoryStream) // the current implementation returns a MemoryStream
                return memoryStream.ToArray(); // then we can just return its value

            var buffer = new byte[body.Length];
            
#if NETSTANDARD
            body.Read(buffer, 0, (int)body.Length);
#else
            body.ReadExactly(buffer, 0, (int)body.Length);
#endif 
            return buffer;
        }

        private static Dictionary<string, object> GetContextBag(IDataReader dr)
        {
            var i = dr.GetOrdinal("HeaderBag");
            var headerBag = dr.IsDBNull(i) ? "" : dr.GetString(i);
            var dictionaryBag =
                JsonSerializer.Deserialize<Dictionary<string, object>>(headerBag, JsonSerialisationOptions.Options);
            return dictionaryBag;
        }

        private static string GetContentType(IDataReader dr)
        {
            var ordinal = dr.GetOrdinal("ContentType");
            if (dr.IsDBNull(ordinal)) return null;

            var contentType = dr.GetString(ordinal);
            return contentType;
        }

        private static string GetCorrelationId(IDataReader dr)
        {
            var ordinal = dr.GetOrdinal("CorrelationId");
            if (dr.IsDBNull(ordinal)) return null;

            var correlationId = dr.GetString(ordinal);
            return correlationId;
        }

        private static MessageType GetMessageType(IDataReader dr)
        {
            return (MessageType)Enum.Parse(typeof(MessageType), dr.GetString(dr.GetOrdinal("MessageType")));
        }

        private static string GetMessageId(IDataReader dr)
        {
            return dr.GetString(dr.GetOrdinal("MessageId"));
        }

        private static string GetPartitionKey(IDataReader dr)
        {
            var ordinal = dr.GetOrdinal("PartitionKey");
            if (dr.IsDBNull(ordinal)) return null;

            var partitionKey = dr.GetString(ordinal);
            return partitionKey;
        }


        private static string GetReplyTo(IDataReader dr)
        {
            var ordinal = dr.GetOrdinal("ReplyTo");
            if (dr.IsDBNull(ordinal)) return null;

            var replyTo = dr.GetString(ordinal);
            return replyTo;
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
    }
}
