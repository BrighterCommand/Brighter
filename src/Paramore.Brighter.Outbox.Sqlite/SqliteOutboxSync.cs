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
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Sqlite;

namespace Paramore.Brighter.Outbox.Sqlite
{
    /// <summary>
    /// Implements an outbox using Sqlite as a backing store
    /// </summary>
    public class SqliteOutboxSync : RelationDatabaseOutboxSync<SqliteConnection, SqliteCommand, SqliteDataReader, SqliteParameter>
    {
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<SqliteOutboxSync>();

        private const int SqliteDuplicateKeyError = 1555;
        private const int SqliteUniqueKeyError = 19;
        private readonly SqliteConfiguration _configuration;
        private readonly ISqliteConnectionProvider _connectionProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqliteOutboxSync" /> class.
        /// </summary>
        /// <param name="configuration">The configuration to connect to this data store</param>
        /// <param name="connectionProvider">Provides a connection to the Db that allows us to enlist in an ambient transaction</param>
        public SqliteOutboxSync(SqliteConfiguration configuration, ISqliteConnectionProvider connectionProvider) : base(
            configuration.OutBoxTableName, new SqliteQueries(), ApplicationLogging.CreateLogger<SqliteOutboxSync>())
        {
            _configuration = configuration;
            ContinueOnCapturedContext = false;
            _connectionProvider = connectionProvider;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqliteOutboxSync" /> class.
        /// </summary>
        /// <param name="configuration">The configuration to connect to this data store</param>
         public SqliteOutboxSync(SqliteConfiguration configuration) : this(configuration, new SqliteConnectionProvider(configuration))
        {
        }

        protected override void WriteToStore(IAmABoxTransactionConnectionProvider transactionConnectionProvider, Func<SqliteConnection, SqliteCommand> commandFunc,
            Action loggingAction)
        {
            var connectionProvider = _connectionProvider;
            if (transactionConnectionProvider != null && transactionConnectionProvider is ISqliteTransactionConnectionProvider provider)
                connectionProvider = provider;

            var connection = connectionProvider.GetConnection();

            if (connection.State != ConnectionState.Open)
                connection.Open();
            using (var command = commandFunc.Invoke(connection))
            {
                try
                {
                    if (transactionConnectionProvider != null && connectionProvider.HasOpenTransaction)
                        command.Transaction = connectionProvider.GetTransaction();
                    command.ExecuteNonQuery();
                }
                catch (SqliteException sqlException)
                {
                    if (IsExceptionUnqiueOrDuplicateIssue(sqlException))
                    {
                        loggingAction.Invoke();
                        return;
                    }

                    throw;
                }
                finally
                {
                    if (!connectionProvider.IsSharedConnection)
                        connection.Dispose();
                    else if (!connectionProvider.HasOpenTransaction)
                        connection.Close();
                }
            }
        }

        protected override async Task WriteToStoreAsync(IAmABoxTransactionConnectionProvider transactionConnectionProvider, Func<SqliteConnection, SqliteCommand> commandFunc,
            Action loggingAction, CancellationToken cancellationToken)
        {
            var connectionProvider = _connectionProvider;
            if (transactionConnectionProvider != null && transactionConnectionProvider is ISqliteTransactionConnectionProvider provider)
                connectionProvider = provider;

            var connection = await connectionProvider.GetConnectionAsync(cancellationToken);

            if (connection.State != ConnectionState.Open)
                connection.Open();
            using (var command = commandFunc.Invoke(connection))
            {
                try
                {
                    if (transactionConnectionProvider != null && connectionProvider.HasOpenTransaction)
                        command.Transaction = connectionProvider.GetTransaction();
                    await command.ExecuteNonQueryAsync(cancellationToken);
                }
                catch (SqliteException sqlException)
                {
                    if (IsExceptionUnqiueOrDuplicateIssue(sqlException))
                    {
                        loggingAction.Invoke();
                        return;
                    }

                    throw;
                }
                finally
                {
                    if (!connectionProvider.IsSharedConnection)
                        connection.Dispose();
                    else if (!connectionProvider.HasOpenTransaction)
#if NETSTANDARD2_0
                        connection.Close();
#else
                    await connection.CloseAsync();
#endif
                }
            }
        }

        protected override T ReadFromStore<T>(Func<SqliteConnection, SqliteCommand> commandFunc, Func<SqliteDataReader, T> resultFunc)
        {
            var connection = _connectionProvider.GetConnection();

            if (connection.State != ConnectionState.Open)
                connection.Open();
            using (var command = commandFunc.Invoke(connection))
            {
                try
                {
                    return resultFunc.Invoke(command.ExecuteReader());
                }
                finally
                {
                    if (!_connectionProvider.IsSharedConnection)
                        connection.Dispose();
                    else if (!_connectionProvider.HasOpenTransaction)
                        connection.Close();
                }
            }
        }

        protected override async Task<T> ReadFromStoreAsync<T>(Func<SqliteConnection, SqliteCommand> commandFunc, Func<SqliteDataReader, Task<T>> resultFunc, CancellationToken cancellationToken)
        {
            var connection = await _connectionProvider.GetConnectionAsync(cancellationToken);

            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync(cancellationToken);
            using (var command = commandFunc.Invoke(connection))
            {
                try
                {
                    return await resultFunc.Invoke(await command.ExecuteReaderAsync(cancellationToken));
                }
                finally
                {
                    if (!_connectionProvider.IsSharedConnection)
                        connection.Dispose();
                    else if (!_connectionProvider.HasOpenTransaction)
                        connection.Close();
                }
            }
        }
        
        protected override SqliteCommand CreateCommand(SqliteConnection connection, string sqlText, int outBoxTimeout,
            params SqliteParameter[] parameters)
        {
            var command = connection.CreateCommand();

            command.CommandTimeout = outBoxTimeout < 0 ? 0 : outBoxTimeout;
            command.CommandText = sqlText;
            command.Parameters.AddRange(parameters);

            return command;
        }

        protected override SqliteParameter[] CreatePagedOutstandingParameters(double milliSecondsSinceAdded, int pageSize, int pageNumber)
        {
            var parameters = new SqliteParameter[3];
            parameters[0] = CreateSqlParameter("PageNumber", pageNumber);
            parameters[1] = CreateSqlParameter("PageSize", pageSize);
            parameters[2] = CreateSqlParameter("OutstandingSince", milliSecondsSinceAdded);

            return parameters;
        }

        protected override SqliteParameter CreateSqlParameter(string parameterName, object value)
        {
            return new SqliteParameter(parameterName, value ?? DBNull.Value);
        }

        protected override SqliteParameter[] InitAddDbParameters(Message message, int? position = null)
        {
            var prefix = position.HasValue ? $"p{position}_" : "";
            var bagJson = JsonSerializer.Serialize(message.Header.Bag, JsonSerialisationOptions.Options);
            return new[]
            {
                new SqliteParameter($"@{prefix}MessageId", SqliteType.Text) { Value = message.Id.ToString() },
                new SqliteParameter($"@{prefix}MessageType", SqliteType.Text) { Value = message.Header.MessageType.ToString() },
                new SqliteParameter($"@{prefix}Topic", SqliteType.Text) { Value = message.Header.Topic },
                new SqliteParameter($"@{prefix}Timestamp", SqliteType.Text) { Value = message.Header.TimeStamp.ToString("s") },
                new SqliteParameter($"@{prefix}CorrelationId", SqliteType.Text) { Value = message.Header.CorrelationId },
                new SqliteParameter($"@{prefix}ReplyTo", SqliteType.Text) {Value =  message.Header.ReplyTo}, 
                new SqliteParameter($"@{prefix}ContentType", SqliteType.Text) {Value = message.Header.ContentType},
                new SqliteParameter($"@{prefix}HeaderBag", SqliteType.Text) { Value = bagJson },
                new SqliteParameter($"@{prefix}Body", SqliteType.Text) { Value = message.Body.Value }
            };
        }

        protected override Message MapFunction(SqliteDataReader dr)
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

        protected override async Task<Message> MapFunctionAsync(SqliteDataReader dr, CancellationToken cancellationToken)
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

        protected override IEnumerable<Message> MapListFunction(SqliteDataReader dr)
        {
            var messages = new List<Message>();
            while (dr.Read())
            {
                messages.Add(MapAMessage(dr));
            }
            dr.Close();

            return messages;
        }

        protected override async Task<IEnumerable<Message>> MapListFunctionAsync(SqliteDataReader dr, CancellationToken cancellationToken)
        {
            var messages = new List<Message>();
            while (await dr.ReadAsync(cancellationToken))
            {
                messages.Add(MapAMessage(dr));
            }
            dr.Close();

            return messages;
        }

        protected override async Task<int> MapOutstandingCountAsync(SqliteDataReader dr, CancellationToken cancellationToken)
        {
            int outstadingMessages = -1;
            if (await dr.ReadAsync(cancellationToken))
            {
                outstadingMessages = dr.GetInt32(0);
            }
            dr.Close();

            return outstadingMessages;
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
                DateTime timeStamp = GetTimeStamp(dr);
                var correlationId = GetCorrelationId(dr);
                var replyTo = GetReplyTo(dr);
                var contentType = GetContentType(dr);

                header = new MessageHeader(
                    messageId: id,
                    topic: topic,
                    messageType: messageType,
                    timeStamp: timeStamp,
                    handledCount: 0,
                    delayedMilliseconds: 0,
                    correlationId: correlationId,
                    replyTo: replyTo,
                    contentType: contentType);

                Dictionary<string, object> dictionaryBag = GetContextBag(dr);
                if (dictionaryBag != null)
                {
                    foreach (var key in dictionaryBag.Keys)
                    {
                        header.Bag.Add(key, dictionaryBag[key]);
                    }
                }
            }

            var body = new MessageBody(dr.GetString(dr.GetOrdinal("Body")));

            return new Message(header, body);
        }

        private static string GetTopic(IDataReader dr)
        {
            return dr.GetString(dr.GetOrdinal("Topic"));
        }

        private static MessageType GetMessageType(IDataReader dr)
        {
            return (MessageType)Enum.Parse(typeof(MessageType), dr.GetString(dr.GetOrdinal("MessageType")));
        }

        private static Guid GetMessageId(IDataReader dr)
        {
            return Guid.Parse(dr.GetString(dr.GetOrdinal("MessageId")));
        }

        private string GetContentType(IDataReader dr)
        {
            var ordinal = dr.GetOrdinal("ContentType");
            if (dr.IsDBNull(ordinal)) return null;

            var replyTo = dr.GetString(ordinal);
            return replyTo;
        }

        private string GetReplyTo(IDataReader dr)
        {
            var ordinal = dr.GetOrdinal("ReplyTo");
            if (dr.IsDBNull(ordinal)) return null;

            var replyTo = dr.GetString(ordinal);
            return replyTo;
        }

        private static Dictionary<string, object> GetContextBag(IDataReader dr)
        {
            var i = dr.GetOrdinal("HeaderBag");
            var headerBag = dr.IsDBNull(i) ? "" : dr.GetString(i);
            var dictionaryBag = JsonSerializer.Deserialize<Dictionary<string, object>>(headerBag, JsonSerialisationOptions.Options);
            return dictionaryBag;
        }

        private Guid? GetCorrelationId(IDataReader dr)
        {
            var ordinal = dr.GetOrdinal("CorrelationId");
            if (dr.IsDBNull(ordinal)) return null;

            var correlationId = dr.GetGuid(ordinal);
            return correlationId;
        }

        private static DateTime GetTimeStamp(IDataReader dr)
        {
            var ordinal = dr.GetOrdinal("Timestamp");
            var timeStamp = dr.IsDBNull(ordinal)
                ? DateTime.MinValue
                : dr.GetDateTime(ordinal);
            return timeStamp;
        }
        
    }
}
