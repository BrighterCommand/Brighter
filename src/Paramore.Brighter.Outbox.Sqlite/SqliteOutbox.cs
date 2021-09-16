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
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.Outbox.Sqlite
{
    /// <summary>
    ///     Class SqliteOutbox.
    /// </summary>
    public class SqliteOutbox :
        IAmAnOutbox<Message>,
        IAmAnOutboxAsync<Message>,
        IAmAnOutboxViewer<Message>,
        IAmAnOutboxViewerAsync<Message>
    {
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<SqliteOutbox>();

        private const int SqliteDuplicateKeyError = 1555;
        private const int SqliteUniqueKeyError = 19;
        private readonly SqliteOutboxConfiguration _configuration;

        /// <summary>
        ///     If false we the default thread synchronization context to run any continuation, if true we re-use the original
        ///     synchronization context.
        ///     Default to false unless you know that you need true, as you risk deadlocks with the originating thread if you Wait
        ///     or access the Result or otherwise block. You may need the orginating synchronization context if you need to access
        ///     thread specific storage
        ///     such as HTTPContext
        /// </summary>
        public bool ContinueOnCapturedContext { get; set; }

        /// <summary>
        ///     Initializes a new instance of the <see cref="SqliteOutbox" /> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        public SqliteOutbox(SqliteOutboxConfiguration configuration)
        {
            _configuration = configuration;
            ContinueOnCapturedContext = false;
        }

        /// <summary>
        ///     Adds the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="outBoxTimeout"></param>
        /// <returns>Task.</returns>
        public void Add(Message message, int outBoxTimeout = -1, IAmABoxTransactionConnectionProvider transactionConnectionProvider = null)
        {
            var parameters = InitAddDbParameters(message);

            using (var connection = GetConnection())
            {
                connection.Open();
                var sql = GetAddSql();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    AddParamtersParamArrayToCollection(parameters.ToArray(), command);

                    try
                    {
                        command.ExecuteNonQuery();
                    }
                    catch (SqliteException sqlException)
                    {
                        if (IsExceptionUnqiueOrDuplicateIssue(sqlException))
                        {
                            s_logger.LogWarning(
                                "MsSqlOutbox: A duplicate Message with the MessageId {Id} was inserted into the Outbox, ignoring and continuing",
                                message.Id);
                            return;
                        }

                        throw;
                    }
                }
            }
        }

        public async Task AddAsync(Message message, int outBoxTimeout = -1, CancellationToken cancellationToken = default(CancellationToken), IAmABoxTransactionConnectionProvider transactionConnectionProvider = null)
        {
            var parameters = InitAddDbParameters(message);

            using (var connection = GetConnection())
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
                using (var command = connection.CreateCommand())
                {
                    var sql = GetAddSql();
                    command.CommandText = sql;
                    AddParamtersParamArrayToCollection(parameters.ToArray(), command);

                    try
                    {
                        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
                    }
                    catch (SqliteException sqlException)
                    {
                        if (IsExceptionUnqiueOrDuplicateIssue(sqlException))
                        {
                            s_logger.LogWarning("MsSqlOutbox: A duplicate Message with the MessageId {Id} was inserted into the Outbox, ignoring and continuing",
                                message.Id);
                            return;
                        }

                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Get the messages that have been marked as flushed in the store
        /// </summary>
        /// <param name="millisecondsDispatchedSince">How long ago would the message have been dispatched in milliseconds</param>
        /// <param name="pageSize">How many messages in a page</param>
        /// <param name="pageNumber">Which page of messages to get</param>
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
            using (var connection = GetConnection())
            using (var command = connection.CreateCommand())
            {
                CreatePagedDispatchedCommand(command, millisecondsDispatchedSince, pageSize, pageNumber);

                connection.Open();

                var dbDataReader = command.ExecuteReader();

                var messages = new List<Message>();
                while (dbDataReader.Read())
                {
                    messages.Add(MapAMessage(dbDataReader));
                }
                return messages;
            }
        }

        private string GetAddSql()
        {
            var sql =
                string.Format(
                    "INSERT INTO {0} (MessageId, MessageType, Topic, Timestamp, CorrelationId, ReplyTo, ContentType, HeaderBag, Body) VALUES (@MessageId, @MessageType, @Topic, @Timestamp, @CorrelationId, @ReplyTo, @ContentType, @HeaderBag, @Body)",
                    _configuration.OutboxTableName);
            return sql;
        }

        /// <summary>
        /// Gets the specified message
        /// </summary>
        /// <param name="messageId">The message identifier.</param>
        /// <param name="outBoxTimeout">Timeout for the outbox read, defaults to library default timeout</param>
        /// <returns>The message</returns>
        public Message Get(Guid messageId, int outBoxTimeout = -1)
        {
            var sql = string.Format("SELECT * FROM {0} WHERE MessageId = @MessageId", _configuration.OutboxTableName);
            var parameters = new[]
            {
                CreateSqlParameter("@MessageId", messageId.ToString())
            };

            return ExecuteCommand(command => MapFunction(command.ExecuteReader()), sql, outBoxTimeout, parameters);
        }

        /// <summary>
        /// Update a message to show it is dispatched
        /// </summary>
        /// <param name="id">The id of the message to update</param>
        /// <param name="dispatchedAt">When was the message dispatched, defaults to UTC now</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        public async Task MarkDispatchedAsync(Guid id, DateTime? dispatchedAt = null, Dictionary<string, object> args = null, CancellationToken cancellationToken = default)
        {
           using (var connection = GetConnection())
           {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
                using (var command = InitMarkDispatchedCommand(connection, id, dispatchedAt))
                {
                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
                }
           }
        }

        /// <summary>
        /// Update a message to show it is dispatched
        /// </summary>
        /// <param name="id">The id of the message to update</param>
        /// <param name="dispatchedAt">When was the message dispatched, defaults to UTC now</param>
        public void MarkDispatched(Guid id, DateTime? dispatchedAt = null, Dictionary<string, object> args = null)
        {
           using (var connection = GetConnection())
           {
                connection.Open();
                using (var command = InitMarkDispatchedCommand(connection, id, dispatchedAt))
                {
                    command.ExecuteNonQuery();
                }
           }
        }


        /// <summary>
        /// get as an asynchronous operation.
        /// </summary>
        /// <param name="messageId">The message identifier.</param>
        /// <param name="outBoxTimeout">The time allowed for the read in milliseconds; on  a -2 default</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        /// <returns>A Message.</returns>
        public async Task<Message> GetAsync(Guid messageId, int outBoxTimeout = -1, CancellationToken cancellationToken = default(CancellationToken))
        {
            var sql = string.Format("SELECT * FROM {0} WHERE MessageId = @MessageId", _configuration.OutboxTableName);
            var parameters = new[]
            {
                CreateSqlParameter("@MessageId", messageId.ToString())
            };

            return await ExecuteCommandAsync(
                    async command => MapFunction(await command.ExecuteReaderAsync(cancellationToken)
                        .ConfigureAwait(ContinueOnCapturedContext)),
                    sql,
                    outBoxTimeout,
                    cancellationToken,
                    parameters)
                .ConfigureAwait(ContinueOnCapturedContext);
        }


        /// <summary>
        /// Returns all messages in the outbox
        /// </summary>
        /// <param name="pageSize">Number of messages to return in search results (default = 100)</param>
        /// <param name="pageNumber">Page number of results to return (default = 1)</param>
        /// <param name="args">Additional parameters required for search, if any</param>
         /// <returns>A page of messages from the outbox</returns>
       public IList<Message> Get(int pageSize = 100, int pageNumber = 1, Dictionary<string, object> args = null)
        {
            using (var connection = GetConnection())
            using (var command = connection.CreateCommand())
            {
                CreatePagedRead(command, pageSize, pageNumber);

                connection.Open();

                using (var dbDataReader = command.ExecuteReader())
                {
                    var messages = new List<Message>();
                    while (dbDataReader.Read())
                    {
                        messages.Add(MapAMessage(dbDataReader));
                    }

                    return messages;
                }
            }
        }

        /// <summary>
        /// Returns all messages in the store
        /// </summary>
        /// <param name="pageSize">Number of messages to return in search results (default = 100)</param>
        /// <param name="pageNumber">Page number of results to return (default = 1)</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
         /// <param name="args">Additional parameters required for search, if any</param>
        /// <returns>A list of messages</returns>
        public async Task<IList<Message>> GetAsync(
            int pageSize = 100, 
            int pageNumber = 1, 
            Dictionary<string, object> args = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            using (var connection = GetConnection())
            using (var command = connection.CreateCommand())
            {
                CreatePagedRead(command, pageSize, pageNumber);

                await connection.OpenAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);

                var messages = new List<Message>();
                using (var dbDataReader = await command.ExecuteReaderAsync(cancellationToken)
                    .ConfigureAwait(ContinueOnCapturedContext))
                {
                    while (await dbDataReader.ReadAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext))
                    {
                        messages.Add(MapAMessage(dbDataReader));
                    }
                }

                ;
                return messages;
            }
        }

        /// <summary>
        /// Retrieves those messages that have not been dispatched to the broker in a time period
        /// since they were added to the outbox
        /// </summary>
        /// <param name="millSecondsSinceSent">How long ago where they added to the outbox</param>
        /// <param name="pageSize">How many messages per page</param>
        /// <param name="pageNumber">How many pages</param>
         /// <param name="args">Additional parameters required for search, if any</param>
        /// <returns>A list of outstanding messages</returns>
        public IEnumerable<Message> OutstandingMessages(
            double millSecondsSinceSent, 
            int pageSize = 100, 
            int pageNumber = 1,
             Dictionary<string, object> args = null)
        {
            using (var connection = GetConnection())
            using (var command = connection.CreateCommand())
            {
                CreatePagedOutstandingCommand(command, millSecondsSinceSent, pageSize, pageNumber);

                connection.Open();

                var dbDataReader = command.ExecuteReader();

                var messages = new List<Message>();
                while (dbDataReader.Read())
                {
                    messages.Add(MapAMessage(dbDataReader));
                }
                return messages;
            }
        }

        /// <summary>
        /// Retrieves those messages that have not been dispatched to the broker in a time period
        /// since they were added to the outbox
        /// </summary>
        /// <param name="millSecondsSinceSent">How long ago where they added to the outbox</param>
        /// <param name="pageSize">How many messages per page</param>
        /// <param name="pageNumber">How many pages</param>
        /// <param name="args">Additional parameters required for search, if any</param>
        /// <param name="cancellationToken">Async Cancellation Token</param>
        /// <returns>A list of outstanding messages</returns>
        public async Task<IEnumerable<Message>> OutstandingMessagesAsync(
            double millSecondsSinceSent, 
            int pageSize = 100, 
            int pageNumber = 1,
            Dictionary<string, object> args = null,
            CancellationToken cancellationToken = default)
        {
            using (var connection = GetConnection())
            using (var command = connection.CreateCommand())
            {
                CreatePagedOutstandingCommand(command, millSecondsSinceSent, pageSize, pageNumber);

                await connection.OpenAsync(cancellationToken);

                var dbDataReader = await command.ExecuteReaderAsync(cancellationToken);

                var messages = new List<Message>();
                while (await dbDataReader.ReadAsync())
                {
                    messages.Add(MapAMessage(dbDataReader));
                }
                return messages;
            }
        }

        private void AddParamtersParamArrayToCollection(SqliteParameter[] parameters, SqliteCommand command)
        {
            command.Parameters.AddRange(parameters);
        }
        
        private void CreatePagedDispatchedCommand(SqliteCommand command, double millisecondsDispatchedSince, int pageSize, int pageNumber)
        {
            var pagingSqlFormat = "SELECT * FROM {0} AS TBL WHERE DISPATCHED IS NOT NULL AND DISPATCHED < DATEADD(millisecond, @OutStandingSince, getdate()) ORDER BY Timestamp DESC limit @PageSize OFFSET @PageNumber";
            var parameters = new[]
            {
                CreateSqlParameter("PageNumber", pageNumber),
                CreateSqlParameter("PageSize", pageSize),
                CreateSqlParameter("OutstandingSince", -1 * millisecondsDispatchedSince)
            };

            var sql = string.Format(pagingSqlFormat, _configuration.OutboxTableName);

            command.CommandText = sql;
            command.Parameters.AddRange(parameters);
        }
        
        private void CreatePagedRead(SqliteCommand command, int pageSize, int pageNumber)
        {
            SqliteParameter[] parameters = new[]
            {
                CreateSqlParameter("PageNumber", pageNumber - 1),
                CreateSqlParameter("PageSize", pageSize)
            };

            var sql = string.Format("SELECT * FROM {0} ORDER BY Timestamp DESC limit @PageSize OFFSET @PageNumber",
                _configuration.OutboxTableName);

            command.CommandText = sql;
            AddParamtersParamArrayToCollection(parameters, command);
        }

        private void CreatePagedOutstandingCommand(SqliteCommand command, double milliSecondsSinceAdded, int pageSize, int pageNumber)
        {
            var pagingSqlFormat = "SELECT * FROM {0} AS TBL WHERE DISPATCHED IS NULL AND TIMESTAMP < DATEADD(millisecond, -@OutStandingSince, getdate()) ORDER BY Timestamp ASC limit @PageSize OFFSET @PageNumber";
            var parameters = new[]
            {
                CreateSqlParameter("PageNumber", pageNumber),
                CreateSqlParameter("PageSize", pageSize),
                CreateSqlParameter("OutstandingSince", milliSecondsSinceAdded)
            };

            var sql = string.Format(pagingSqlFormat, _configuration.OutboxTableName);

            command.CommandText = sql;
            command.Parameters.AddRange(parameters);
        }

        private SqliteParameter CreateSqlParameter(string parameterName, object value)
        {
            return new SqliteParameter(parameterName, value);
        }

        private T ExecuteCommand<T>(Func<SqliteCommand, T> execute, string sql, int outboxTimeout,
            params SqliteParameter[] parameters)
        {
            using (var connection = GetConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                AddParamtersParamArrayToCollection(parameters, command);

                if (outboxTimeout != -1) command.CommandTimeout = outboxTimeout;

                connection.Open();
                var item = execute(command);
                return item;
            }
        }

        private async Task<T> ExecuteCommandAsync<T>(
            Func<SqliteCommand, Task<T>> execute,
            string sql,
            int timeoutInMilliseconds,
            CancellationToken cancellationToken = default(CancellationToken),
            params SqliteParameter[] parameters)
        {
            using (var connection = GetConnection())
            using (var command = connection.CreateCommand())
            {
                if (timeoutInMilliseconds != -1) command.CommandTimeout = timeoutInMilliseconds;
                command.CommandText = sql;
                AddParamtersParamArrayToCollection(parameters, command);

                await connection.OpenAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
                var item = await execute(command).ConfigureAwait(ContinueOnCapturedContext);
                return item;
            }
        }

        private SqliteConnection GetConnection()
        {
            return new SqliteConnection(_configuration.ConnectionString);
        }

        private SqliteParameter[] InitAddDbParameters(Message message)
        {
            var bagJson = JsonSerializer.Serialize(message.Header.Bag, JsonSerialisationOptions.Options);
            return new[]
            {
                new SqliteParameter("@MessageId", SqliteType.Text) {Value = message.Id.ToString()},
                new SqliteParameter("@MessageType", SqliteType.Text) {Value = message.Header.MessageType.ToString()},
                new SqliteParameter("@Topic", SqliteType.Text) {Value = message.Header.Topic},
                new SqliteParameter("@Timestamp", SqliteType.Text) {Value = message.Header.TimeStamp.ToString("s")},
                new SqliteParameter("CorrelationId", SqliteType.Text) { Value = message.Header.CorrelationId},
                new SqliteParameter("ReplyTo", message.Header.ReplyTo),
                new SqliteParameter("ContentType", message.Header.ContentType),
                new SqliteParameter("@HeaderBag", SqliteType.Text) {Value = bagJson},
                new SqliteParameter("@Body", SqliteType.Text) {Value = message.Body.Value}
            };
        }

        private SqliteCommand InitMarkDispatchedCommand(SqliteConnection connection, Guid messageId, DateTime? dispatchedAt)
        {
            var command = connection.CreateCommand();
            var sql = $"UPDATE {_configuration.OutboxTableName} SET Dispatched = @DispatchedAt WHERE MessageId = @mMessageId";
            command.CommandText = sql;
            command.Parameters.Add(CreateSqlParameter("MessageId", messageId));
            command.Parameters.Add(CreateSqlParameter("DispatchedAt", dispatchedAt));
            return command;
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
                    messageId:id, 
                    topic:topic, 
                    messageType:messageType, 
                    timeStamp:timeStamp, 
                    handledCount:0, 
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
            return (MessageType) Enum.Parse(typeof (MessageType), dr.GetString(dr.GetOrdinal("MessageType")));
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


        private Message MapFunction(IDataReader dr)
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

        private void SetPagingCommandFor(SqliteCommand command, int pageSize, int pageNumber)
        {
            SqliteParameter[] parameters = new[]
            {
                CreateSqlParameter("PageNumber", pageNumber-1),
                CreateSqlParameter("PageSize", pageSize)
            };

            var sql = string.Format("SELECT * FROM {0} ORDER BY Timestamp DESC limit @PageSize OFFSET @PageNumber", _configuration.OutboxTableName);

            command.CommandText = sql;
            AddParamtersParamArrayToCollection(parameters, command);
        }
   }
}
