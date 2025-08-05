#region Licence

/* The MIT License (MIT)
Copyright © 2014 Francesco Pighi <francesco.pighi@gmail.com>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
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
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using Paramore.Brighter.Logging;
using Paramore.Brighter.PostgreSql;

namespace Paramore.Brighter.Outbox.PostgreSql
{
#pragma warning disable CS0618
    public class PostgreSqlOutboxAsync : IAmABulkOutboxAsync<Message>
#pragma warning restore CS0618
    {
        private readonly PostgreSqlOutboxConfiguration _configuration;
        private readonly IPostgreSqlConnectionProvider _connectionProvider;
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<PostgreSqlOutboxAsync>();
        
        private readonly string _outboxTableName;
        
        public bool ContinueOnCapturedContext { get; set; }

        /// <summary>
        /// Initialises a new instance of <see cref="PostgreSqlOutboxAsync"> class.
        /// </summary>
        /// <param name="configuration">PostgreSql Outbox Configuration.</param>
        /// <param name="connectionProvider">The connection provider for PostgreSQL</param>
        public PostgreSqlOutboxAsync(PostgreSqlOutboxConfiguration configuration, IPostgreSqlConnectionProvider connectionProvider = null)
        {
            _configuration = configuration;
            _connectionProvider = connectionProvider ?? new PostgreSqlNpgsqlConnectionProvider(configuration);
            _outboxTableName = configuration.OutboxTableName;
        }

        /// <summary>
        /// Awaitable add the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="outBoxTimeout">The time allowed for the write in milliseconds; on a -1 default</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        /// <param name="transactionConnectionProvider">The Connection Provider to use for this call</param>
        public async Task AddAsync(Message message, int outBoxTimeout = -1, CancellationToken cancellationToken = default, IAmABoxTransactionConnectionProvider transactionConnectionProvider = null)
        {
            var connectionProvider = GetConnectionProvider(transactionConnectionProvider);
            var parameters = InitAddDbParameters(message);
            var connection = await GetOpenConnectionAsync(connectionProvider, cancellationToken);

            try
            {
                using (var command = InitAddDbCommand(connection, parameters))
                {
                    if (connectionProvider.HasOpenTransaction)
                        command.Transaction = connectionProvider.GetTransaction();
                    
                    if (outBoxTimeout != -1)
                        command.CommandTimeout = outBoxTimeout;
                        
                    await command.ExecuteNonQueryAsync(cancellationToken);
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
                    await connection.DisposeAsync();
                else if (!connectionProvider.HasOpenTransaction)
                    await connection.CloseAsync();
            }
        }
        
        /// <summary>
        /// Awaitable add the specified messages.
        /// </summary>
        /// <param name="messages">The messages.</param>
        /// <param name="outBoxTimeout">The time allowed for the write in milliseconds; on a -1 default</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        /// <param name="transactionConnectionProvider">The Connection Provider to use for this call</param>
        public async Task AddAsync(IEnumerable<Message> messages, int outBoxTimeout = -1, CancellationToken cancellationToken = default, IAmABoxTransactionConnectionProvider transactionConnectionProvider = null)
        {
            var connectionProvider = GetConnectionProvider(transactionConnectionProvider);
            var connection = await GetOpenConnectionAsync(connectionProvider, cancellationToken);

            try
            {
                using (var command = InitBulkAddDbCommand(connection, messages.ToList()))
                {
                    if (connectionProvider.HasOpenTransaction)
                        command.Transaction = connectionProvider.GetTransaction();
                        
                    if (outBoxTimeout != -1)
                        command.CommandTimeout = outBoxTimeout;
                        
                    await command.ExecuteNonQueryAsync(cancellationToken);
                }
            }
            catch (PostgresException sqlException)
            {
                if (sqlException.SqlState == PostgresErrorCodes.UniqueViolation)
                {
                    s_logger.LogWarning(
                        "PostgresSQLOutbox: A duplicate Message was found in the batch");
                    return;
                }

                throw;
            }
            finally
            {
                if (!connectionProvider.IsSharedConnection)
                    await connection.DisposeAsync();
                else if (!connectionProvider.HasOpenTransaction)
                    await connection.CloseAsync();
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
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        /// <returns>A list of dispatched messages</returns>
        public async Task<IEnumerable<Message>> DispatchedMessagesAsync(
            double millisecondsDispatchedSince,
            int pageSize = 100,
            int pageNumber = 1,
            int outboxTimeout = -1,
            Dictionary<string, object> args = null,
            CancellationToken cancellationToken = default)
        {
            var connectionProvider = GetConnectionProvider();
            var connection = await GetOpenConnectionAsync(connectionProvider, cancellationToken);

            try
            {
                using (var command = InitPagedDispatchedCommand(connection, millisecondsDispatchedSince, pageSize, pageNumber))
                {
                    if (outboxTimeout != -1)
                        command.CommandTimeout = outboxTimeout;
                        
                    var messages = new List<Message>();

                    using (var dbDataReader = await command.ExecuteReaderAsync(cancellationToken))
                    {
                        while (await dbDataReader.ReadAsync(cancellationToken))
                            messages.Add(MapAMessage(dbDataReader));
                    }

                    return messages;
                }
            }
            finally
            {
                if (!connectionProvider.IsSharedConnection)
                    await connection.DisposeAsync();
                else if (!connectionProvider.HasOpenTransaction)
                    await connection.CloseAsync();
            }
        }

        /// <summary>
        /// Returns all messages in the store
        /// </summary>
        /// <param name="pageSize">Number of messages to return in search results (default = 100)</param>
        /// <param name="pageNumber">Page number of results to return (default = 1)</param>
        /// <param name="args">Additional parameters required for search, if any</param>
        /// <param name="cancellationToken">Cancellation Token, if any</param>
        /// <returns>A list of messages</returns>
        public async Task<IList<Message>> GetAsync(int pageSize = 100, int pageNumber = 1, Dictionary<string, object> args = null, CancellationToken cancellationToken = default)
        {
            var connectionProvider = GetConnectionProvider();
            var connection = await GetOpenConnectionAsync(connectionProvider, cancellationToken);

            try
            {
                using (var command = InitPagedReadCommand(connection, pageSize, pageNumber))
                {
                    var messages = new List<Message>();

                    using (var dbDataReader = await command.ExecuteReaderAsync(cancellationToken))
                    {
                        while (await dbDataReader.ReadAsync(cancellationToken))
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
                    await connection.DisposeAsync();
                else if (!connectionProvider.HasOpenTransaction)
                    await connection.CloseAsync();
            }
        }

        /// <summary>
        /// Awaitable get the specified message identifier.
        /// </summary>
        /// <param name="messageId">The message identifier.</param>
        /// <param name="outBoxTimeout">The time allowed for the read in milliseconds; on  a -2 default</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        /// <returns>The message</returns>
        public async Task<Message> GetAsync(Guid messageId, int outBoxTimeout = -1, CancellationToken cancellationToken = default)
        {
            var sql = string.Format(PostgreSqlOutboxQueries.GetMessageByIdCommand, _configuration.OutboxTableName);
            var parameters = new[] { InitNpgsqlParameter("MessageId", messageId) };

            return await ExecuteCommandAsync(async command => await MapFunctionAsync(command, cancellationToken), sql, outBoxTimeout, parameters, cancellationToken);
        }

        /// <summary>
        /// Awaitable get the messages.
        /// </summary>
        /// <param name="messageIds">The message identifiers.</param>
        /// <param name="outBoxTimeout">The time allowed for the read in milliseconds; on  a -2 default</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        /// <returns>The messages</returns>
        public async Task<IEnumerable<Message>> GetAsync(IEnumerable<Guid> messageIds, int outBoxTimeout = -1, CancellationToken cancellationToken = default)
        {
            var messages = new List<Message>();
            foreach (var messageId in messageIds)
            {
                var message = await GetAsync(messageId, outBoxTimeout, cancellationToken);
                if (message.Header.MessageType != MessageType.MT_NONE)
                {
                    messages.Add(message);
                }
            }
            return messages;
        }

        /// <summary>
        /// Update a message to show it is dispatched
        /// </summary>
        /// <param name="id">The id of the message to update</param>
        /// <param name="dispatchedAt">When was the message dispatched, defaults to UTC now</param>
        /// <param name="args">Additional parameters required for the update</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        public async Task MarkDispatchedAsync(Guid id, DateTime? dispatchedAt = null, Dictionary<string, object> args = null, CancellationToken cancellationToken = default)
        {
            var connectionProvider = GetConnectionProvider();
            var connection = await GetOpenConnectionAsync(connectionProvider, cancellationToken);

            try
            {
                using (var command = InitMarkDispatchedCommand(connection, id, dispatchedAt))
                {
                    await command.ExecuteNonQueryAsync(cancellationToken);
                }
            }
            finally
            {
                if (!connectionProvider.IsSharedConnection)
                    await connection.DisposeAsync();
                else if (!connectionProvider.HasOpenTransaction)
                    await connection.CloseAsync();
            }
        }

        /// <summary>
        /// Update messages to show they are dispatched
        /// </summary>
        /// <param name="ids">The ids of the messages to update</param>
        /// <param name="dispatchedAt">When was the message dispatched, defaults to UTC now</param>
        /// <param name="args">Additional parameters required for the update</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        public async Task MarkDispatchedAsync(IEnumerable<Guid> ids, DateTime? dispatchedAt = null, Dictionary<string, object> args = null, CancellationToken cancellationToken = default)
        {
            foreach (var id in ids)
            {
                await MarkDispatchedAsync(id, dispatchedAt, args, cancellationToken);
            }
        }

        /// <summary>
        /// Returns messages that have yet to be dispatched
        /// </summary>
        /// <param name="millSecondsSinceSent">How long ago as the message sent?</param>
        /// <param name="pageSize">How many messages to return at once?</param>
        /// <param name="pageNumber">Which page number of messages</param>
        /// <param name="args">Additional parameters required for search, if any</param>
        /// <param name="cancellationToken">Async Cancellation Token</param>
        /// <returns>A list of messages that are outstanding for dispatch</returns>
        public async Task<IEnumerable<Message>> OutstandingMessagesAsync(
             double millSecondsSinceSent,
             int pageSize = 100,
             int pageNumber = 1,
             Dictionary<string, object> args = null,
             CancellationToken cancellationToken = default)
        {
            var connectionProvider = GetConnectionProvider();
            var connection = await GetOpenConnectionAsync(connectionProvider, cancellationToken);

            try
            {
                using (var command = InitPagedOutstandingCommand(connection, millSecondsSinceSent, pageSize, pageNumber))
                {
                    var messages = new List<Message>();

                    using (var dbDataReader = await command.ExecuteReaderAsync(cancellationToken))
                    {
                        while (await dbDataReader.ReadAsync(cancellationToken))
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
                    await connection.DisposeAsync();
                else if (!connectionProvider.HasOpenTransaction)
                    await connection.CloseAsync();
            }
        }

        /// <summary>
        /// Delete the specified messages
        /// </summary>
        /// <param name="messageIds">The id of the message to delete</param>
        /// <param name="cancellationToken">The Cancellation Token</param>
        public async Task DeleteAsync(Guid[] messageIds, CancellationToken cancellationToken)
        {
            await WriteToStoreAsync(null, connection => InitDeleteDispatchedCommand(connection, messageIds), null, cancellationToken);
        }
        
        /// <summary>
        /// Get the messages that have been dispatched
        /// </summary>
        /// <param name="hoursDispatchedSince">The number of hours since the message was dispatched</param>
        /// <param name="pageSize">The amount to return</param>
        /// <param name="cancellationToken">The Cancellation Token</param>
        /// <returns>Messages that have already been dispatched</returns>
        public async Task<IEnumerable<Message>> DispatchedMessagesAsync(
            int hoursDispatchedSince,
            int pageSize = 100,
            CancellationToken cancellationToken = default)
        {
            var millisecondsDispatchedSince = hoursDispatchedSince * 60 * 60 * 1000;
            return await DispatchedMessagesAsync(millisecondsDispatchedSince, pageSize, 1, -1, null, cancellationToken);
        }

        /// <summary>
        /// Gets the number of un dispatched messages in the outbox
        /// </summary>
        /// <param name="cancellationToken">The Cancellation Token</param>
        /// <returns>Number of messages in the outbox that have yet to be dispatched</returns>
        public async Task<int> GetNumberOfOutstandingMessagesAsync(CancellationToken cancellationToken)
        {
            var sql = $"SELECT COUNT(*) FROM {_configuration.OutboxTableName} WHERE DISPATCHED IS NULL";
            
            var connectionProvider = GetConnectionProvider();
            var connection = await GetOpenConnectionAsync(connectionProvider, cancellationToken);

            try
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    var result = await command.ExecuteScalarAsync(cancellationToken);
                    return Convert.ToInt32(result);
                }
            }
            finally
            {
                if (!connectionProvider.IsSharedConnection)
                    await connection.DisposeAsync();
                else if (!connectionProvider.HasOpenTransaction)
                    await connection.CloseAsync();
            }
        }

        #region Helper Methods

        private NpgsqlCommand InitDeleteDispatchedCommand(NpgsqlConnection connection, IEnumerable<Guid> messageIds)
        {
            var inClause = GenerateInClauseAndAddParameters(messageIds.ToList());
            foreach (var p in inClause.parameters)
            {
                p.DbType = DbType.Object;
            }
            return CreateCommand(connection, GenerateSqlText(PostgreSqlOutboxQueries.DeleteMessageCommand, inClause.inClause), 0,
                inClause.parameters);
        }
        
        private (string inClause, NpgsqlParameter[] parameters) GenerateInClauseAndAddParameters(List<Guid> messageIds)
        {
            var paramNames = messageIds.Select((s, i) => "@p" + i).ToArray();

            var parameters = new NpgsqlParameter[messageIds.Count];
            for (int i = 0; i < paramNames.Count(); i++)
            {
                parameters[i] = CreateSqlParameter(paramNames[i], messageIds[i]);
            }

            return (string.Join(",", paramNames), parameters);
        }
        
        private NpgsqlParameter CreateSqlParameter(string parameterName, object value)
        {
            return new NpgsqlParameter(parameterName, value ?? DBNull.Value);
        }
        
        private string GenerateSqlText(string sqlFormat, params string[] orderedParams)
            => string.Format(sqlFormat, orderedParams.Prepend(_outboxTableName).ToArray());
        
        private NpgsqlCommand CreateCommand(NpgsqlConnection connection, string sqlText, int outBoxTimeout,
            params NpgsqlParameter[] parameters)

        {
            var command = connection.CreateCommand();

            command.CommandTimeout = outBoxTimeout < 0 ? 0 : outBoxTimeout;
            command.CommandText = sqlText;
            command.Parameters.AddRange(parameters);

            return command;
        }
        
        private async Task WriteToStoreAsync(IAmABoxTransactionConnectionProvider transactionConnectionProvider, Func<NpgsqlConnection, NpgsqlCommand> commandFunc, Action loggingAction, CancellationToken cancellationToken)
        {
            var connectionProvider = _connectionProvider;
            if (transactionConnectionProvider != null && transactionConnectionProvider is IPostgreSqlConnectionProvider provider)
                connectionProvider = provider;

            var connection = connectionProvider.GetConnection();

            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync(cancellationToken);
                
            using (var command = commandFunc.Invoke(connection))
            {
                try
                {
                    if (transactionConnectionProvider != null && connectionProvider.HasOpenTransaction)
                        command.Transaction = connectionProvider.GetTransaction();
                    await command.ExecuteNonQueryAsync(cancellationToken);
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
                    if (!connectionProvider.IsSharedConnection)
                        await connection.DisposeAsync();
                    else if (!connectionProvider.HasOpenTransaction)
                        await connection.CloseAsync();
                }
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

        private async Task<NpgsqlConnection> GetOpenConnectionAsync(IPostgreSqlConnectionProvider connectionProvider, CancellationToken cancellationToken)
        {
            NpgsqlConnection connection = connectionProvider.GetConnection();

            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync(cancellationToken);

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

            command.CommandText = string.Format(PostgreSqlOutboxQueries.PagedDispatchedCommand, _configuration.OutboxTableName);
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

            command.CommandText = string.Format(PostgreSqlOutboxQueries.PagedReadCommand, _configuration.OutboxTableName);
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

            command.CommandText = string.Format(PostgreSqlOutboxQueries.PagedOutstandingCommand, _configuration.OutboxTableName);
            command.Parameters.AddRange(parameters);

            return command;
        }

        private NpgsqlParameter[] InitAddDbParameters(Message message, int? position = null)
        {
            var prefix = position.HasValue ? $"p{position}_" : "";
            var bagjson = JsonSerializer.Serialize(message.Header.Bag, JsonSerialisationOptions.Options);
            return new NpgsqlParameter[]
            {
                InitNpgsqlParameter($"{prefix}MessageId", message.Id),
                InitNpgsqlParameter($"{prefix}MessageType", message.Header.MessageType.ToString()),
                InitNpgsqlParameter($"{prefix}Topic", message.Header.Topic),
                new NpgsqlParameter($"{prefix}Timestamp", NpgsqlDbType.TimestampTz) {Value = message.Header.TimeStamp},
                InitNpgsqlParameter($"{prefix}CorrelationId", message.Header.CorrelationId),
                InitNpgsqlParameter($"{prefix}ReplyTo", message.Header.ReplyTo),
                InitNpgsqlParameter($"{prefix}ContentType", message.Header.ContentType),
                InitNpgsqlParameter($"{prefix}HeaderBag", bagjson),
                InitNpgsqlParameter($"{prefix}Body", message.Body.Value)
            };
        }

        private NpgsqlCommand InitMarkDispatchedCommand(NpgsqlConnection connection, Guid messageId,
            DateTime? dispatchedAt)
        {
            var command = connection.CreateCommand();
            command.CommandText = string.Format(PostgreSqlOutboxQueries.MarkDispatchedCommand, _configuration.OutboxTableName);
            command.Parameters.Add(InitNpgsqlParameter("MessageId", messageId));
            command.Parameters.Add(InitNpgsqlParameter("DispatchedAt", dispatchedAt));
            return command;
        }

        private async Task<T> ExecuteCommandAsync<T>(Func<NpgsqlCommand, Task<T>> execute, string sql, int messageStoreTimeout,
            NpgsqlParameter[] parameters, CancellationToken cancellationToken)
        {
            var connectionProvider = GetConnectionProvider();
            var connection = await GetOpenConnectionAsync(connectionProvider, cancellationToken);

            try
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    command.Parameters.AddRange(parameters);

                    if (messageStoreTimeout != -1)
                        command.CommandTimeout = messageStoreTimeout;

                    return await execute(command);
                }
            }
            finally
            {
                if (!connectionProvider.IsSharedConnection)
                    await connection.DisposeAsync();
                else if (!connectionProvider.HasOpenTransaction)
                    await connection.CloseAsync();
            }
        }

        private NpgsqlCommand InitAddDbCommand(NpgsqlConnection connection, NpgsqlParameter[] parameters)
        {
            var command = connection.CreateCommand();

            command.CommandText = string.Format(PostgreSqlOutboxQueries.AddMessageCommand, _configuration.OutboxTableName);
            command.Parameters.AddRange(parameters);

            return command;
        }
        
        private NpgsqlCommand InitBulkAddDbCommand(NpgsqlConnection connection, List<Message> messages)
        {
            var messageParams = new List<string>();
            var parameters = new List<NpgsqlParameter>();
            
            for (int i = 0; i < messages.Count; i++)
            {
                messageParams.Add($"(@p{i}_MessageId, @p{i}_MessageType, @p{i}_Topic, @p{i}_Timestamp, @p{i}_CorrelationId, @p{i}_ReplyTo, @p{i}_ContentType, @p{i}_HeaderBag, @p{i}_Body)");
                parameters.AddRange(InitAddDbParameters(messages[i], i));
                
            }
            var sql = string.Format(PostgreSqlOutboxQueries.BulkAddMessageCommand, _configuration.OutboxTableName, string.Join(",", messageParams));
            
            var command = connection.CreateCommand();
            
            command.CommandText = sql;
            command.Parameters.AddRange(parameters.ToArray());

            return command;
        }

        private async Task<Message> MapFunctionAsync(NpgsqlCommand command, CancellationToken cancellationToken)
        {
            using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                if (await reader.ReadAsync(cancellationToken))
                {
                    return MapAMessage(reader);
                }
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

        #endregion
    }
}
