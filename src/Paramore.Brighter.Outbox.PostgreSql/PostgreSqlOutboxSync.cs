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
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using Paramore.Brighter.Logging;
using Paramore.Brighter.PostgreSql;

namespace Paramore.Brighter.Outbox.PostgreSql
{
    public class PostgreSqlOutboxSync : IAmAnOutboxSync<Message>, IAmAnOutboxViewer<Message>
    {
        private readonly PostgreSqlOutboxConfiguration _configuration;
        private readonly IPostgreSqlConnectionProvider _connectionProvider;
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<PostgreSqlOutboxSync>();

        public bool ContinueOnCapturedContext
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        /// <summary>
        /// Initialises a new instance of <see cref="PostgreSqlOutboxSync"> class.
        /// </summary>
        /// <param name="postgresSqlOutboxConfiguration">PostgreSql Outbox Configuration.</param>
        public PostgreSqlOutboxSync(PostgreSqlOutboxConfiguration configuration, IPostgreSqlConnectionProvider connectionProvider = null)
        {
            _configuration = configuration;
            _connectionProvider = connectionProvider;
        }

        /// <summary>
        /// Adds the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="outBoxTimeout">The time allowed for the write in milliseconds; on a -1 default</param>
        public void Add(Message message, int outBoxTimeout = -1, IAmABoxTransactionConnectionProvider transactionConnectionProvider = null)
        {
            var connectionProvider = GetConnectionProvider(transactionConnectionProvider);
            var parameters = InitAddDbParameters(message);
            var connection = GetOpenConnection(connectionProvider);

            try
            {
                using (var command = InitAddDbCommand(connection, parameters))
                {
                    command.ExecuteNonQuery();
                }
            }
            catch (PostgresException sqlException)
            {
                if (sqlException.SqlState == PostgresErrorCodes.UniqueViolation)
                {
                    s_logger.LogWarning(
                        "PostgresSQLOutbox: A duplicate Message with the MessageId {Id} was inserted into the Outbox, ignoring and continuing",
                        message.Id);
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

        /// <summary>
        /// Returns messages that have been successfully dispatched
        /// </summary>
        /// <param name="millisecondsDispatchedSince">How long ago was the message dispatched?</param>
        /// <param name="pageSize">How many messages returned at once?</param>
        /// <param name="pageNumber">Which page of the dispatched messages to return?</param>
        /// <param name="outboxTimeout"></param>
        /// <param name="args">Additional parameters required for search, if any</param>
        /// <returns>A list of dispatched messages</returns>
        public IEnumerable<Message> DispatchedMessages(
            double millisecondsDispatchedSince,
            int pageSize = 100,
            int pageNumber = 1,
            int outboxTimeout = -1,
            Dictionary<string, object> args = null)
        {
            var connectionProvider = GetConnectionProvider();
            var connection = GetOpenConnection(connectionProvider);

            try
            {
                using (var command = InitPagedDispatchedCommand(connection, millisecondsDispatchedSince, pageSize, pageNumber))
                {
                    var messages = new List<Message>();

                    using (var dbDataReader = command.ExecuteReader())
                    {
                        while (dbDataReader.Read())
                            messages.Add(MapAMessage(dbDataReader));
                    }

                    return messages;
                }
            }
            finally
            {
                if (!connectionProvider.IsSharedConnection)
                    connection.Dispose();
                else if (!connectionProvider.HasOpenTransaction)
                    connection.Close();
            }
        }

        /// <summary>
        /// Returns all messages in the store
        /// </summary>
        /// <param name="pageSize">Number of messages to return in search results (default = 100)</param>
        /// <param name="pageNumber">Page number of results to return (default = 1)</param>
        /// <param name="args">Additional parameters required for search, if any</param>
        /// <returns>A list of messages</returns>
        public IList<Message> Get(int pageSize = 100, int pageNumber = 1, Dictionary<string, object> args = null)
        {
            var connectionProvider = GetConnectionProvider();
            var connection = GetOpenConnection(connectionProvider);

            try
            {
                using (var command = InitPagedReadCommand(connection, pageSize, pageNumber))
                {
                    var messages = new List<Message>();

                    using (var dbDataReader = command.ExecuteReader())
                    {
                        while (dbDataReader.Read())
                        {
                            messages.Add(MapAMessage(dbDataReader));
                        }
                    }

                    return messages;
                }
            }
            finally
            {
                if (!connectionProvider.IsSharedConnection)
                    connection.Dispose();
                else if (!connectionProvider.HasOpenTransaction)
                    connection.Close();
            }
        }

        /// <summary>
        /// Gets the specified message identifier.
        /// </summary>
        /// <param name="messageId">The message identifier.</param>
        /// <param name="outBoxTimeout">The time allowed for the read in milliseconds; on  a -2 default</param>
        /// <returns>The message</returns>
        public Message Get(Guid messageId, int outBoxTimeout = -1)
        {
            var sql = string.Format(
                "SELECT Id, MessageId, Topic, MessageType, Timestamp, Correlationid, ReplyTo, ContentType, HeaderBag, Body FROM {0} WHERE MessageId = @MessageId",
                _configuration.OutboxTableName);
            var parameters = new[] { InitNpgsqlParameter("MessageId", messageId) };

            return ExecuteCommand(command => MapFunction(command.ExecuteReader()), sql, outBoxTimeout, parameters);
        }

        /// <summary>
        /// Update a message to show it is dispatched
        /// </summary>
        /// <param name="id">The id of the message to update</param>
        /// <param name="dispatchedAt">When was the message dispatched, defaults to UTC now</param>
        public void MarkDispatched(Guid id, DateTime? dispatchedAt = null, Dictionary<string, object> args = null)
        {
            var connectionProvider = GetConnectionProvider();
            var connection = GetOpenConnection(connectionProvider);

            try
            {
                using (var command = InitMarkDispatchedCommand(connection, id, dispatchedAt))
                {
                    command.ExecuteNonQuery();
                }
            }
            finally
            {
                if (!connectionProvider.IsSharedConnection)
                    connection.Dispose();
                else if (!connectionProvider.HasOpenTransaction)
                    connection.Close();
            }
        }

        /// <summary>
        /// Returns messages that have yet to be dispatched
        /// </summary>
        /// <param name="millSecondsSinceSent">How long ago as the message sent?</param>
        /// <param name="pageSize">How many messages to return at once?</param>
        /// <param name="pageNumber">Which page number of messages</param>
        /// <param name="args">Additional parameters required for search, if any</param>
        /// <returns>A list of messages that are outstanding for dispatch</returns>
        public IEnumerable<Message> OutstandingMessages(
             double millSecondsSinceSent,
             int pageSize = 100,
             int pageNumber = 1,
             Dictionary<string, object> args = null)
        {
            var connectionProvider = GetConnectionProvider();
            var connection = GetOpenConnection(connectionProvider);

            try
            {
                using (var command = InitPagedOutstandingCommand(connection, millSecondsSinceSent, pageSize, pageNumber))
                {
                    var messages = new List<Message>();

                    using (var dbDataReader = command.ExecuteReader())
                    {
                        while (dbDataReader.Read())
                        {
                            messages.Add(MapAMessage(dbDataReader));
                        }
                    }

                    return messages;
                }
            }
            finally
            {
                if (!connectionProvider.IsSharedConnection)
                    connection.Dispose();
                else if (!connectionProvider.HasOpenTransaction)
                    connection.Close();
            }
        }

        private IPostgreSqlConnectionProvider GetConnectionProvider(IAmABoxTransactionConnectionProvider transactionConnectionProvider = null)
        {
            var connectionProvider = _connectionProvider ?? new PostgreSqlNpgsqlConnectionProvider(_configuration);

            if (transactionConnectionProvider != null)
            {
                if (transactionConnectionProvider is IPostgreSqlTransactionConnectionProvider provider)
                    connectionProvider = provider;
                else
                    throw new Exception($"{nameof(transactionConnectionProvider)} does not implement interface {nameof(IPostgreSqlTransactionConnectionProvider)}.");
            }

            return connectionProvider;
        }

        private NpgsqlConnection GetOpenConnection(IPostgreSqlConnectionProvider connectionProvider)
        {
            NpgsqlConnection connection = connectionProvider.GetConnection();

            if (connection.State != ConnectionState.Open)
                connection.Open();

            return connection;
        }

        private NpgsqlParameter InitNpgsqlParameter(string parametername, object value)
        {
            if (value != null)
                return new NpgsqlParameter(parametername, value);
            else
                return new NpgsqlParameter(parametername, DBNull.Value);
        }

        private NpgsqlCommand InitPagedDispatchedCommand(NpgsqlConnection connection, double millisecondsDispatchedSince,
            int pageSize, int pageNumber)
        {
            var command = connection.CreateCommand();

            var parameters = new[]
            {
                InitNpgsqlParameter("PageNumber", pageNumber),
                InitNpgsqlParameter("PageSize", pageSize),
                InitNpgsqlParameter("OutstandingSince", -1 * millisecondsDispatchedSince)
            };

            var pagingSqlFormat =
                "SELECT * FROM (SELECT ROW_NUMBER() OVER(ORDER BY Timestamp DESC) AS NUMBER, * FROM {0}) AS TBL WHERE DISPATCHED IS NOT NULL AND DISPATCHED < (CURRENT_TIMESTAMP + (@OutstandingSince || ' millisecond')::INTERVAL) AND NUMBER BETWEEN ((@PageNumber-1)*@PageSize+1) AND (@PageNumber*@PageSize) ORDER BY Timestamp DESC";

            command.CommandText = string.Format(pagingSqlFormat, _configuration.OutboxTableName);
            command.Parameters.AddRange(parameters);

            return command;
        }

        private NpgsqlCommand InitPagedReadCommand(NpgsqlConnection connection, int pageSize, int pageNumber)
        {
            var command = connection.CreateCommand();

            var parameters = new[]
            {
                InitNpgsqlParameter("PageNumber", pageNumber),
                InitNpgsqlParameter("PageSize", pageSize)
            };

            var pagingSqlFormat =
                "SELECT * FROM (SELECT ROW_NUMBER() OVER(ORDER BY Timestamp DESC) AS NUMBER, * FROM {0}) AS TBL WHERE NUMBER BETWEEN ((@PageNumber-1)*@PageSize+1) AND (@PageNumber*@PageSize) ORDER BY Timestamp DESC";

            command.CommandText = string.Format(pagingSqlFormat, _configuration.OutboxTableName);
            command.Parameters.AddRange(parameters);

            return command;
        }

        private NpgsqlCommand InitPagedOutstandingCommand(NpgsqlConnection connection, double milliSecondsSinceAdded, int pageSize,
            int pageNumber)
        {
            var command = connection.CreateCommand();

            var parameters = new[]
            {
                InitNpgsqlParameter("PageNumber", pageNumber),
                InitNpgsqlParameter("PageSize", pageSize),
                InitNpgsqlParameter("OutstandingSince", milliSecondsSinceAdded)
            };

            var pagingSqlFormat =
               "SELECT * FROM (SELECT ROW_NUMBER() OVER(ORDER BY Timestamp ASC) AS NUMBER, * FROM {0} WHERE DISPATCHED IS NULL) AS TBL WHERE TIMESTAMP < (CURRENT_TIMESTAMP + (@OutstandingSince || ' millisecond')::INTERVAL) AND NUMBER BETWEEN ((@PageNumber-1)*@PageSize+1) AND (@PageNumber*@PageSize) ORDER BY Timestamp ASC";

            command.CommandText = string.Format(pagingSqlFormat, _configuration.OutboxTableName);
            command.Parameters.AddRange(parameters);

            return command;
        }

        private NpgsqlParameter[] InitAddDbParameters(Message message)
        {
            var bagjson = JsonSerializer.Serialize(message.Header.Bag, JsonSerialisationOptions.Options);
            return new NpgsqlParameter[]
            {
                InitNpgsqlParameter("MessageId", message.Id),
                InitNpgsqlParameter("MessageType", message.Header.MessageType.ToString()),
                InitNpgsqlParameter("Topic", message.Header.Topic),
                new NpgsqlParameter("Timestamp", NpgsqlDbType.TimestampTz) {Value = message.Header.TimeStamp},
                InitNpgsqlParameter("CorrelationId", message.Header.CorrelationId),
                InitNpgsqlParameter("ReplyTo", message.Header.ReplyTo),
                InitNpgsqlParameter("ContentType", message.Header.ContentType),
                InitNpgsqlParameter("HeaderBag", bagjson),
                InitNpgsqlParameter("Body", message.Body.Value)
            };
        }

        private NpgsqlCommand InitMarkDispatchedCommand(NpgsqlConnection connection, Guid messageId,
            DateTime? dispatchedAt)
        {
            var command = connection.CreateCommand();
            command.CommandText = $"UPDATE {_configuration.OutboxTableName} SET Dispatched = @DispatchedAt WHERE MessageId = @MessageId";
            command.Parameters.Add(InitNpgsqlParameter("MessageId", messageId));
            command.Parameters.Add(InitNpgsqlParameter("DispatchedAt", dispatchedAt));
            return command;
        }

        private T ExecuteCommand<T>(Func<NpgsqlCommand, T> execute, string sql, int messageStoreTimeout,
            NpgsqlParameter[] parameters)
        {
            var connectionProvider = GetConnectionProvider();
            var connection = GetOpenConnection(connectionProvider);

            try
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    command.Parameters.AddRange(parameters);

                    if (messageStoreTimeout != -1)
                        command.CommandTimeout = messageStoreTimeout;

                    return execute(command);
                }
            }
            finally
            {
                if (!connectionProvider.IsSharedConnection)
                    connection.Dispose();
                else if (!connectionProvider.HasOpenTransaction)
                    connection.Close();
            }
        }

        private NpgsqlCommand InitAddDbCommand(NpgsqlConnection connection, NpgsqlParameter[] parameters)
        {
            var command = connection.CreateCommand();

            var addSqlFormat = "INSERT INTO {0} (MessageId, MessageType, Topic, Timestamp, CorrelationId, ReplyTo, ContentType, HeaderBag, Body) VALUES (@MessageId, @MessageType, @Topic, @Timestamp::timestamptz, @CorrelationId, @ReplyTo, @ContentType,  @HeaderBag, @Body)";

            command.CommandText = string.Format(addSqlFormat, _configuration.OutboxTableName);
            command.Parameters.AddRange(parameters);

            return command;
        }

        private Message MapFunction(IDataReader reader)
        {
            if (reader.Read())
            {
                return MapAMessage(reader);
            }

            return new Message();
        }

        private Message MapAMessage(IDataReader dr)
        {
            var id = GetMessageId(dr);
            var messageType = GetMessageType(dr);
            var topic = GetTopic(dr);

            DateTime timeStamp = GetTimeStamp(dr);
            var correlationId = GetCorrelationId(dr);
            var replyTo = GetReplyTo(dr);
            var contentType = GetContentType(dr);

            var header = new MessageHeader(
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
            return dr.GetGuid(dr.GetOrdinal("MessageId"));
        }

        private string GetContentType(IDataReader dr)
        {
            var ordinal = dr.GetOrdinal("ContentType");
            if (dr.IsDBNull(ordinal))
                return null;

            var replyTo = dr.GetString(ordinal);
            return replyTo;
        }

        private string GetReplyTo(IDataReader dr)
        {
            var ordinal = dr.GetOrdinal("ReplyTo");
            if (dr.IsDBNull(ordinal))
                return null;

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
            if (dr.IsDBNull(ordinal))
                return null;

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
