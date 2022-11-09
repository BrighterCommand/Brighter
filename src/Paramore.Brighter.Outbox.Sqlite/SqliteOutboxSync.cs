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
using Paramore.Brighter.Sqlite;

namespace Paramore.Brighter.Outbox.Sqlite
{
    /// <summary>
    /// Implements an outbox using Sqlite as a backing store
    /// </summary>
    public class SqliteOutboxSync :
        IAmABulkOutboxSync<Message>,
        IAmABulkOutboxAsync<Message>
    {
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<SqliteOutboxSync>();

        private const int SqliteDuplicateKeyError = 1555;
        private const int SqliteUniqueKeyError = 19;
        private readonly SqliteConfiguration _configuration;
        private readonly ISqliteConnectionProvider _connectionProvider;

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
        /// Initializes a new instance of the <see cref="SqliteOutboxSync" /> class.
        /// </summary>
        /// <param name="configuration">The configuration to connect to this data store</param>
        /// <param name="connectionProvider">Provides a connection to the Db that allows us to enlist in an ambient transaction</param>
        public SqliteOutboxSync(SqliteConfiguration configuration, ISqliteConnectionProvider connectionProvider)
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

        /// <summary>
        ///     Adds the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="outBoxTimeout"></param>
        /// <returns>Task.</returns>
        public void Add(Message message, int outBoxTimeout = -1, IAmABoxTransactionConnectionProvider transactionConnectionProvider = null)
        {
            var parameters = InitAddDbParameters(message);

            var connectionProvider = _connectionProvider;
            if (transactionConnectionProvider is ISqliteTransactionConnectionProvider provider)
                connectionProvider = provider;

            var connection = connectionProvider.GetConnection();

            if (connection.State != ConnectionState.Open) connection.Open();
            var sql = GetAddSql();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                AddParamtersParamArrayToCollection(parameters.ToArray(), command);

                try
                {
                    if (connectionProvider.HasOpenTransaction)
                        command.Transaction = connectionProvider.GetTransaction();
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
                finally
                {
                    if(!connectionProvider.IsSharedConnection) connection.Dispose();
                    else if (!connectionProvider.HasOpenTransaction) connection.Close();
                }
            }
        }

        public async Task AddAsync(Message message, int outBoxTimeout = -1, CancellationToken cancellationToken = default(CancellationToken),
            IAmABoxTransactionConnectionProvider transactionConnectionProvider = null)
        {
            var parameters = InitAddDbParameters(message);

            var connectionProvider = _connectionProvider;
            if (transactionConnectionProvider is ISqliteTransactionConnectionProvider provider)
                connectionProvider = provider;

            var connection = await connectionProvider.GetConnectionAsync(cancellationToken);

            if (connection.State != ConnectionState.Open) await connection.OpenAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);

            await using (var command = connection.CreateCommand())
            {
                var sql = GetAddSql();
                command.CommandText = sql;
                AddParamtersParamArrayToCollection(parameters.ToArray(), command);

                try
                {
                    if (connectionProvider.IsSharedConnection)
                        command.Transaction = connectionProvider.GetTransaction();
                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
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
                finally
                {
                    if(!connectionProvider.IsSharedConnection) connection.Dispose();
                    else if (!connectionProvider.HasOpenTransaction) await connection.CloseAsync();
                }
            }
        }
        
        /// <summary>
        /// Awaitable add the specified message.
        /// </summary>
        /// <param name="messages">The message.</param>
        /// <param name="outBoxTimeout">The time allowed for the write in milliseconds; on a -1 default</param>
        /// <param name="transactionConnectionProvider">The Connection Provider to use for this call</param>
        public void Add(IEnumerable<Message> messages, int outBoxTimeout = -1,
            IAmABoxTransactionConnectionProvider transactionConnectionProvider = null)
        {
            var connectionProvider = _connectionProvider;
            if (transactionConnectionProvider is ISqliteTransactionConnectionProvider provider)
                connectionProvider = provider;

            var connection = connectionProvider.GetConnection();

            if (connection.State != ConnectionState.Open) connection.Open();
            var sql = GetBulkAddSql(messages.ToList());
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql.sql;
                AddParamtersParamArrayToCollection(sql.parameters, command);

                try
                {
                    if (connectionProvider.HasOpenTransaction)
                        command.Transaction = connectionProvider.GetTransaction();
                    command.ExecuteNonQuery();
                }
                catch (SqliteException sqlException)
                {
                    if (IsExceptionUnqiueOrDuplicateIssue(sqlException))
                    {
                        s_logger.LogWarning(
                            "MsSqlOutbox: A duplicate Message was detected in the batch");
                        return;
                    }

                    throw;
                }
                finally
                {
                    if(!connectionProvider.IsSharedConnection) connection.Dispose();
                    else if (!connectionProvider.HasOpenTransaction) connection.Close();
                }
            }
        }

        /// <summary>
        /// Interface IAmABulkOutboxAsync
        /// In order to provide reliability for messages sent over an external bus we store the message into an OutBox to 
        /// allow later replay of those messages in the event of failure. We automatically copy any posted message into the store
        /// We provide implementations of <see cref="IAmAnOutboxAsync{T}"/> for various databases. Users using unsupported 
        /// databases should consider a Pull request
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public async Task AddAsync(IEnumerable<Message> messages, int outBoxTimeout = -1,
            CancellationToken cancellationToken = default(CancellationToken),
            IAmABoxTransactionConnectionProvider transactionConnectionProvider = null)
        {
            var connectionProvider = _connectionProvider;
            if (transactionConnectionProvider is ISqliteTransactionConnectionProvider provider)
                connectionProvider = provider;

            var connection = await connectionProvider.GetConnectionAsync(cancellationToken);

            if (connection.State != ConnectionState.Open) await connection.OpenAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);

            var sql = GetBulkAddSql(messages.ToList());
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = sql.sql;
                AddParamtersParamArrayToCollection(sql.parameters, command);

                try
                {
                    if (connectionProvider.IsSharedConnection)
                        command.Transaction = connectionProvider.GetTransaction();
                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
                }
                catch (SqliteException sqlException)
                {
                    if (IsExceptionUnqiueOrDuplicateIssue(sqlException))
                    {
                        s_logger.LogWarning(
                            "MsSqlOutbox: A duplicate Message was detected in the batch");
                        return;
                    }

                    throw;
                }
                finally
                {
                    if(!connectionProvider.IsSharedConnection) connection.Dispose();
                    else if (!connectionProvider.HasOpenTransaction) await connection.CloseAsync();
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
            var connection = _connectionProvider.GetConnection();
            using (var command = connection.CreateCommand())
            {
                CreatePagedDispatchedCommand(command, millisecondsDispatchedSince, pageSize, pageNumber);

                if (connection.State != ConnectionState.Open) connection.Open();

                var dbDataReader = command.ExecuteReader();

                var messages = new List<Message>();
                while (dbDataReader.Read())
                {
                    messages.Add(MapAMessage(dbDataReader));
                }
                dbDataReader.Close();
                
                if(!_connectionProvider.IsSharedConnection) connection.Dispose();
                else if (!_connectionProvider.HasOpenTransaction) connection.Close();

                return messages;
            }
        }

       /// <summary>
        /// Gets the specified message
        /// </summary>
        /// <param name="messageId">The message identifier.</param>
        /// <param name="outBoxTimeout">Timeout for the outbox read, defaults to library default timeout</param>
        /// <returns>The message</returns>
        public Message Get(Guid messageId, int outBoxTimeout = -1)
        {
            var sql = $"SELECT * FROM {_configuration.OutBoxTableName} WHERE MessageId = @MessageId";
            var parameters = new[] { CreateSqlParameter("@MessageId", messageId.ToString()) };

            return ExecuteCommand(command => MapFunction(command.ExecuteReader()), sql, outBoxTimeout, parameters);
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
            var sql = $"SELECT * FROM {_configuration.OutBoxTableName} WHERE MessageId = @MessageId";
            var parameters = new[] { CreateSqlParameter("@MessageId", messageId.ToString()) };

            return await ExecuteCommandAsync(
                    async command => MapFunction(await command.ExecuteReaderAsync(cancellationToken)
                        .ConfigureAwait(ContinueOnCapturedContext)),
                    sql,
                    outBoxTimeout,
                    cancellationToken,
                    parameters)
                .ConfigureAwait(ContinueOnCapturedContext);
        }

        public async Task<IEnumerable<Message>> GetAsync(IEnumerable<Guid> messageIds, int outBoxTimeout = -1,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var connection = await _connectionProvider.GetConnectionAsync(cancellationToken);
            await using (var command = connection.CreateCommand())
            {
                var inClause = string.Join(",", messageIds.ToList().Select((s, i) => "'" + s + "'").ToArray());
                var sql = $"SELECT * FROM {_configuration.OutBoxTableName} WHERE MessageId IN ( {inClause} )";

                command.CommandText = sql;

                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);

                var messages = new List<Message>();
                var dbDataReader = await command.ExecuteReaderAsync(cancellationToken)
                    .ConfigureAwait(ContinueOnCapturedContext);
                
                    while (await dbDataReader.ReadAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext))
                    {
                        messages.Add(MapAMessage(dbDataReader));
                    }
                    dbDataReader.Close();
                
                    if(!_connectionProvider.IsSharedConnection) connection.Dispose();
                    else if (!_connectionProvider.HasOpenTransaction) await connection.CloseAsync();
                    
                return messages;
            }
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
            var connection = _connectionProvider.GetConnection();
            using (var command = connection.CreateCommand())
            {
                CreatePagedRead(command, pageSize, pageNumber);

                if (connection.State != ConnectionState.Open) connection.Open();

                var dbDataReader = command.ExecuteReader();

                var messages = new List<Message>();
                while (dbDataReader.Read())
                {
                    messages.Add(MapAMessage(dbDataReader));
                }

                dbDataReader.Close();

                if (!_connectionProvider.IsSharedConnection) connection.Dispose();
                else if (!_connectionProvider.HasOpenTransaction) connection.Close();

                return messages;
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
            var connection = await _connectionProvider.GetConnectionAsync(cancellationToken);
            await using (var command = connection.CreateCommand())
            {
                CreatePagedRead(command, pageSize, pageNumber);

                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);

                var messages = new List<Message>();
                var dbDataReader = await command.ExecuteReaderAsync(cancellationToken)
                    .ConfigureAwait(ContinueOnCapturedContext);

                while (await dbDataReader.ReadAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext))
                {
                    messages.Add(MapAMessage(dbDataReader));
                }
                dbDataReader.Close();

                if (!_connectionProvider.IsSharedConnection) connection.Dispose();
                else if (!_connectionProvider.HasOpenTransaction) await connection.CloseAsync();

                return messages;
            }
        }


        /// <summary>
        /// Update a message to show it is dispatched
        /// </summary>
        /// <param name="id">The id of the message to update</param>
        /// <param name="dispatchedAt">When was the message dispatched, defaults to UTC now</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        public Task MarkDispatchedAsync(Guid id, DateTime? dispatchedAt = null, Dictionary<string, object> args = null,
            CancellationToken cancellationToken = default)
        {
            return MarkDispatchedAsync(new[] {id}, dispatchedAt, args, cancellationToken);
        }

        public async Task MarkDispatchedAsync(IEnumerable<Guid> ids, DateTime? dispatchedAt = null,
            Dictionary<string, object> args = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var connection = await _connectionProvider.GetConnectionAsync(cancellationToken);
            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
            await using (var command = InitMarkDispatchedCommand(connection, ids, dispatchedAt))
            {
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
            }
            if (!_connectionProvider.IsSharedConnection) connection.Dispose();
            else if (!_connectionProvider.HasOpenTransaction) await connection.CloseAsync();
        }

        /// <summary>
        /// Update a message to show it is dispatched
        /// </summary>
        /// <param name="id">The id of the message to update</param>
        /// <param name="dispatchedAt">When was the message dispatched, defaults to UTC now</param>
        public void MarkDispatched(Guid id, DateTime? dispatchedAt = null, Dictionary<string, object> args = null)
        {
            var connection = _connectionProvider.GetConnection();
            if (connection.State != ConnectionState.Open) connection.Open();
            using (var command = InitMarkDispatchedCommand(connection, new [] {id}, dispatchedAt))
            {
                command.ExecuteNonQuery();
            }
            if (!_connectionProvider.IsSharedConnection) connection.Dispose();
            else if (!_connectionProvider.HasOpenTransaction) connection.Close();
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
            var connection = _connectionProvider.GetConnection();
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
                dbDataReader.Close();
                
                if(!_connectionProvider.IsSharedConnection) connection.Dispose();
                else if (!_connectionProvider.HasOpenTransaction) connection.Close();

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
            var connection = await _connectionProvider.GetConnectionAsync(cancellationToken);
            await using (var command = connection.CreateCommand())
            {
                CreatePagedOutstandingCommand(command, millSecondsSinceSent, pageSize, pageNumber);

                if (connection.State != ConnectionState.Open) await connection.OpenAsync(cancellationToken);

                var dbDataReader = await command.ExecuteReaderAsync(cancellationToken);

                var messages = new List<Message>();
                while (await dbDataReader.ReadAsync(cancellationToken))
                {
                    messages.Add(MapAMessage(dbDataReader));
                }
                dbDataReader.Close();
                
                if(!_connectionProvider.IsSharedConnection) connection.Dispose();
                else if (!_connectionProvider.HasOpenTransaction) await connection.CloseAsync();
                return messages;
            }
        }

        private void AddParamtersParamArrayToCollection(SqliteParameter[] parameters, SqliteCommand command)
        {
            command.Parameters.AddRange(parameters);
        }

        private void CreatePagedDispatchedCommand(SqliteCommand command, double millisecondsDispatchedSince, int pageSize, int pageNumber)
        {
            double fractionalSeconds = millisecondsDispatchedSince / 1000.000;
            var sql =
                $"SELECT * FROM {_configuration.OutBoxTableName} AS TBL WHERE DISPATCHED IS NOT NULL AND DISPATCHED < datetime('now', '-{fractionalSeconds} seconds') ORDER BY Timestamp DESC limit @PageSize OFFSET @PageNumber";
            var parameters = new[]
            {
                CreateSqlParameter("PageNumber", pageNumber), CreateSqlParameter("PageSize", pageSize),
                CreateSqlParameter("OutstandingSince", -1 * millisecondsDispatchedSince)
            };

            command.CommandText = sql;
            command.Parameters.AddRange(parameters);
        }

        private void CreatePagedRead(SqliteCommand command, int pageSize, int pageNumber)
        {
            SqliteParameter[] parameters = new[] { CreateSqlParameter("PageNumber", pageNumber - 1), CreateSqlParameter("PageSize", pageSize) };

            var sql = $"SELECT * FROM {_configuration.OutBoxTableName} ORDER BY Timestamp DESC limit @PageSize OFFSET @PageNumber";

            command.CommandText = sql;
            AddParamtersParamArrayToCollection(parameters, command);
        }

        private void CreatePagedOutstandingCommand(SqliteCommand command, double milliSecondsSinceAdded, int pageSize, int pageNumber)
        {
            double fractionalSeconds = milliSecondsSinceAdded / 1000.000;
            var sql =
                $"SELECT * FROM {_configuration.OutBoxTableName} AS TBL WHERE DISPATCHED IS NULL AND TIMESTAMP < datetime('now', '-{fractionalSeconds} seconds') ORDER BY Timestamp ASC limit @PageSize OFFSET @PageNumber";
            var parameters = new[]
            {
                CreateSqlParameter("PageNumber", pageNumber), CreateSqlParameter("PageSize", pageSize),
                CreateSqlParameter("OutstandingSince", milliSecondsSinceAdded)
            };

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
            var connection = _connectionProvider.GetConnection();
            using (var command = connection.CreateCommand())
            {
                if (connection.State != ConnectionState.Open) connection.Open();
                
                command.CommandText = sql;
                AddParamtersParamArrayToCollection(parameters, command);

                if (outboxTimeout != -1) command.CommandTimeout = outboxTimeout;

                var item = execute(command);
                
                if(!_connectionProvider.IsSharedConnection) connection.Dispose();
                else if (!_connectionProvider.HasOpenTransaction) connection.Close();
                
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
            var connection = await _connectionProvider.GetConnectionAsync(cancellationToken);

            await using (var command = connection.CreateCommand())
            {
                if (connection.State != ConnectionState.Open) await connection.OpenAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
                
                if (timeoutInMilliseconds != -1) command.CommandTimeout = timeoutInMilliseconds;
                command.CommandText = sql;
                AddParamtersParamArrayToCollection(parameters, command);

                var item = await execute(command).ConfigureAwait(ContinueOnCapturedContext);
                
                if(!_connectionProvider.IsSharedConnection) connection.Dispose();
                else if (!_connectionProvider.HasOpenTransaction) await connection.CloseAsync();
                
                return item;
            }
        }
        private string GetAddSql()
        {
            var sql = $"INSERT INTO {_configuration.OutBoxTableName} (MessageId, MessageType, Topic, Timestamp, CorrelationId, ReplyTo, ContentType, HeaderBag, Body) VALUES (@MessageId, @MessageType, @Topic, @Timestamp, @CorrelationId, @ReplyTo, @ContentType, @HeaderBag, @Body)";
            return sql;
        }
        
        private (string sql, SqliteParameter[] parameters) GetBulkAddSql(List<Message> messages)
        {
            var messageParams = new List<string>();
            var parameters = new List<SqliteParameter>();
            
            for (int i = 0; i < messages.Count(); i++)
            {
                messageParams.Add($"(@p{i}_MessageId, @p{i}_MessageType, @p{i}_Topic, @p{i}_Timestamp, @p{i}_CorrelationId, @p{i}_ReplyTo, @p{i}_ContentType, @p{i}_HeaderBag, @p{i}_Body)");
                parameters.AddRange(InitAddDbParameters(messages[i], i));
                
            }
            var sql = $"INSERT INTO {_configuration.OutBoxTableName} (MessageId, MessageType, Topic, Timestamp, CorrelationId, ReplyTo, ContentType, HeaderBag, Body) VALUES {string.Join(",", messageParams)}";
            
            return (sql, parameters.ToArray());
        }

        private SqliteParameter[] InitAddDbParameters(Message message, int? position = null)
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
                new SqliteParameter($"@{prefix}ReplyTo", message.Header.ReplyTo), 
                new SqliteParameter($"@{prefix}ContentType", message.Header.ContentType),
                new SqliteParameter($"@{prefix}HeaderBag", SqliteType.Text) { Value = bagJson },
                new SqliteParameter($"@{prefix}Body", SqliteType.Text) { Value = message.Body.Value }
            };
        }

        private SqliteCommand InitMarkDispatchedCommand(SqliteConnection connection, IEnumerable<Guid> messageIds, DateTime? dispatchedAt)
        {
            var command = connection.CreateCommand();
            var inClause = string.Join(",", messageIds.ToList().Select((s, i) => "'" + s + "'").ToArray());
            var dispatchTime = dispatchedAt.HasValue ? "datetime('" + dispatchedAt.Value.ToString("yyyy-MM-dd HH:mm:ss") + "')" : "datetime('now')"; 
            var sql = $"UPDATE {_configuration.OutBoxTableName} SET Dispatched =  {dispatchTime} WHERE MessageId IN ( {inClause} )";
            command.CommandText = sql;
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
            SqliteParameter[] parameters = new[] { CreateSqlParameter("PageNumber", pageNumber - 1), CreateSqlParameter("PageSize", pageSize) };

            var sql = $"SELECT * FROM {_configuration.OutBoxTableName} ORDER BY Timestamp DESC limit @PageSize OFFSET @PageNumber";

            command.CommandText = sql;
            AddParamtersParamArrayToCollection(parameters, command);
        }
    }
}
