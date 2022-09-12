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
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Paramore.Brighter.Logging;
using Paramore.Brighter.MySql;

namespace Paramore.Brighter.Outbox.MySql
{
    /// <summary>
    ///     Class MySqlOutbox.
    /// </summary>
    public class MySqlOutboxSync :
        IAmABulkOutboxSync<Message>,
        IAmABulkOutboxAsync<Message>
    {
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<MySqlOutboxSync>();

        private const int MySqlDuplicateKeyError = 1062;
        private readonly MySqlConfiguration _configuration;
        private readonly IMySqlConnectionProvider _connectionProvider;

        /// <summary>
        ///     Initializes a new instance of the <see cref="MySqlOutboxSync" /> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <summary>
        ///     If false we the default thread synchronization context to run any continuation, if true we re-use the original
        ///     synchronization context.
        ///     Default to false unless you know that you need true, as you risk deadlocks with the originating thread if you Wait
        ///     or access the Result or otherwise block. You may need the orginating synchronization context if you need to access
        ///     thread specific storage
        ///     such as HTTPContext
        /// </summary>
        public bool ContinueOnCapturedContext { get; set; }

        public MySqlOutboxSync(MySqlConfiguration configuration, IMySqlConnectionProvider connectionProvider)
        {
            _configuration = configuration;
            _connectionProvider = connectionProvider;
            ContinueOnCapturedContext = false;
        }

        public MySqlOutboxSync(MySqlConfiguration configuration) : this(configuration, new MySqlConnectionProvider(configuration))
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
            if (transactionConnectionProvider != null && transactionConnectionProvider is IMySqlTransactionConnectionProvider provider)
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
                catch (MySqlException sqlException)
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

            if (transactionConnectionProvider != null && transactionConnectionProvider is IMySqlTransactionConnectionProvider provider)
                connectionProvider = provider;

            var connection = await connectionProvider.GetConnectionAsync(cancellationToken);


            if (connection.State != ConnectionState.Open) await connection.OpenAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
            using (var command = connection.CreateCommand())
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
                catch (MySqlException sqlException)
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
                    if (!connectionProvider.IsSharedConnection) connection.Dispose();
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
            if (transactionConnectionProvider != null && transactionConnectionProvider is IMySqlTransactionConnectionProvider provider)
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
                catch (MySqlException sqlException)
                {
                    if (IsExceptionUnqiueOrDuplicateIssue(sqlException))
                    {
                        s_logger.LogWarning(
                            "MsSqlOutbox: A duplicate was detected in the batch");
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
        /// Awaitable add the specified message.
        /// </summary>
        /// <param name="messages">The message.</param>
        /// <param name="outBoxTimeout">The time allowed for the write in milliseconds; on a -1 default</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        /// <param name="transactionConnectionProvider">The Connection Provider to use for this call</param>
        /// <returns><see cref="Task"/>.</returns>
        public async Task AddAsync(IEnumerable<Message> messages, int outBoxTimeout = -1,
            CancellationToken cancellationToken = default(CancellationToken),
            IAmABoxTransactionConnectionProvider transactionConnectionProvider = null)
        {
            var connectionProvider = _connectionProvider;

            if (transactionConnectionProvider != null && transactionConnectionProvider is IMySqlTransactionConnectionProvider provider)
                connectionProvider = provider;

            var connection = await connectionProvider.GetConnectionAsync(cancellationToken);


            if (connection.State != ConnectionState.Open) await connection.OpenAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);

            var sql = GetBulkAddSql(messages.ToList());
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql.sql;
                AddParamtersParamArrayToCollection(sql.parameters, command);

                try
                {
                    if (connectionProvider.IsSharedConnection)
                        command.Transaction = connectionProvider.GetTransaction();
                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
                }
                catch (MySqlException sqlException)
                {
                    if (IsExceptionUnqiueOrDuplicateIssue(sqlException))
                    {
                        s_logger.LogWarning(
                            "MsSqlOutbox: A duplicate was detected in the batch");
                        return;
                    }

                    throw;
                }
                finally
                {
                    if (!connectionProvider.IsSharedConnection) connection.Dispose();
                    else if (!connectionProvider.HasOpenTransaction) await connection.CloseAsync();
                }
            }
        }

        /// <summary>
        /// Which messages have been dispatched from the Outbox, within the specified time window
        /// </summary>
        /// <param name="millisecondsDispatchedSince">How long ago can the message have been dispatched</param>
        /// <param name="pageSize">How many messages per page</param>
        /// <param name="pageNumber">Which page number to return</param>
        /// <param name="outboxTimeout">When do we give up?</param>
        /// <param name="args">Additional parameters required for search, if any</param>
        /// <returns>Messages that have been dispatched from the Outbox to the broker</returns>
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
        /// Get a message
        /// </summary>
        /// <param name="messageId">The id of the message to retrieve</param>
        /// <param name="outBoxTimeout">Timeout in milleseconds</param>
        /// <returns></returns>
        public Message Get(Guid messageId, int outBoxTimeout = -1)
        {
            var sql = string.Format("SELECT * FROM {0} WHERE MessageId = @MessageId", _configuration.OutBoxTableName);
            var parameters = new[] { CreateSqlParameter("@MessageId", messageId.ToString()) };

            return ExecuteCommand(command => MapFunction(command.ExecuteReader()), sql, outBoxTimeout, parameters);
        }

        /// <summary>
        /// get as an asynchronous operation.
        /// </summary>
        /// <param name="messageId">The message identifier.</param>
        /// <param name="outBoxTimeout">The time allowed for the read in milliseconds; on  a -2 default</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        /// <returns><see cref="Task{Message}" />.</returns>
        public async Task<Message> GetAsync(
            Guid messageId,
            int outBoxTimeout = -1,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var sql = string.Format("SELECT * FROM {0} WHERE MessageId = @MessageId", _configuration.OutBoxTableName);
            var parameters = new[] { CreateSqlParameter("@MessageId", messageId.ToString()) };

            return await ExecuteCommandAsync(
                    async command => MapFunction(await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext)),
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

            using (var command = connection.CreateCommand())
            {
                CreateBulkReadCommand(command, messageIds);

                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);

                var dbDataReader = await command.ExecuteReaderAsync(cancellationToken)
                    .ConfigureAwait(ContinueOnCapturedContext);
                
                var messages = new List<Message>();

                while (await dbDataReader.ReadAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext))
                {
                    messages.Add(MapAMessage(dbDataReader));
                }
                dbDataReader.Close();

                if (!_connectionProvider.IsSharedConnection)
                    connection.Dispose();
                else if (!_connectionProvider.HasOpenTransaction)
                    await connection.CloseAsync();

                return messages;
            }
        }


        /// <summary>
        /// Returns all messages in the store
        /// </summary>
        /// <param name="pageSize">Number of messages to return in search results (default = 100)</param>
        /// <param name="pageNumber">Page number of results to return (default = 1)</param>
        /// <param name="args">Additional parameters required for search, if any</param>
        /// <returns></returns>
        public IList<Message> Get(int pageSize = 100, int pageNumber = 1, Dictionary<string, object> args = null)
        {
            var connection = _connectionProvider.GetConnection();

            using (var command = connection.CreateCommand())
            {
                CreatePagedReadCommand(command, pageSize, pageNumber);

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
        /// Returns all messages in the store
        /// </summary>
        /// <param name="pageSize">Number of messages to return in search results (default = 100)</param>
        /// <param name="pageNumber">Page number of results to return (default = 1)</param>
        /// <param name="args">Additional parameters required for search, if any</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        /// <returns></returns>
        public async Task<IList<Message>> GetAsync(
            int pageSize = 100,
            int pageNumber = 1,
            Dictionary<string, object> args = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var connection = await _connectionProvider.GetConnectionAsync(cancellationToken);
            
            using (var command = connection.CreateCommand())
            {
                CreatePagedReadCommand(command, pageSize, pageNumber);

                if (connection.State != ConnectionState.Open) await connection.OpenAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);

                var dbDataReader = await command.ExecuteReaderAsync(cancellationToken)
                    .ConfigureAwait(ContinueOnCapturedContext);
                
                var messages = new List<Message>();
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

        public async Task MarkDispatchedAsync(IEnumerable<Guid> ids, DateTime? dispatchedAt = null, Dictionary<string, object> args = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var connection = await _connectionProvider.GetConnectionAsync(cancellationToken);
            
            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
            using (var command = InitMarkDispatchedCommand(connection, ids, dispatchedAt ?? DateTime.UtcNow))
            {
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
            }
            if (!_connectionProvider.IsSharedConnection)
                connection.Dispose();
            else if (!_connectionProvider.HasOpenTransaction)
                await connection.CloseAsync();
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
            using (var command = InitMarkDispatchedCommand(connection, new [] {id}, dispatchedAt ?? DateTime.UtcNow))
            {
                command.ExecuteNonQuery();
            }
            if(!_connectionProvider.IsSharedConnection) connection.Dispose();
            else if (!_connectionProvider.HasOpenTransaction) connection.Close();
        }

        /// <summary>
        /// Messages still outstanding in the Outbox because their timestamp
        /// </summary>
        /// <param name="millSecondsSinceSent">How many seconds since the message was sent do we wait to declare it outstanding</param>
        /// <param name="args">Additional parameters required for search, if any</param>
        /// <returns>Outstanding Messages</returns>
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


        private void AddParamtersParamArrayToCollection(MySqlParameter[] parameters, DbCommand command)
        {
            command.Parameters.AddRange(parameters);
        }

        /// <summary>
        /// Messages still outstanding in the Outbox because their timestamp
        /// </summary>
        /// <param name="millSecondsSinceSent">How many seconds since the message was sent do we wait to declare it outstanding</param>
        /// <param name="args">Additional parameters required for search, if any</param>
        /// <param name="cancellationToken">Async Cancellation Token</param>
        /// <returns>Outstanding Messages</returns>
        public async Task<IEnumerable<Message>> OutstandingMessagesAsync(
            double millSecondsSinceSent, 
            int pageSize = 100, 
            int pageNumber = 1,
            Dictionary<string, object> args = null,
            CancellationToken cancellationToken = default)
        {
            var connection = await _connectionProvider.GetConnectionAsync(cancellationToken);
            using (var command = connection.CreateCommand())
            {
                CreatePagedOutstandingCommand(command, millSecondsSinceSent, pageSize, pageNumber);

                if (connection.State!= ConnectionState.Open) await connection.OpenAsync(cancellationToken);

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

        private MySqlParameter CreateSqlParameter(string parameterName, object value)
        {
            return new MySqlParameter { ParameterName = parameterName, Value = value };
        }

        private void CreatePagedDispatchedCommand(DbCommand command, double millisecondsDispatchedSince, int pageSize, int pageNumber)
        {
            var pagingSqlFormat =
                "SELECT * FROM {0} AS TBL WHERE `CreatedID` BETWEEN ((@PageNumber-1)*@PageSize+1) AND (@PageNumber*@PageSize) AND DISPATCHED IS NOT NULL AND DISPATCHED < DATE_ADD(UTC_TIMESTAMP(), INTERVAL -@OutstandingSince MICROSECOND) AND NUMBER BETWEEN ((@PageNumber-1)*@PageSize+1) AND (@PageNumber*@PageSize) ORDER BY Timestamp DESC";
            var parameters = new[]
            {
                CreateSqlParameter("@PageNumber", pageNumber), CreateSqlParameter("@PageSize", pageSize),
                CreateSqlParameter("@OutstandingSince", millisecondsDispatchedSince * 1000) //we need to add microseconds
            };

            var sql = string.Format(pagingSqlFormat, _configuration.OutBoxTableName);

            command.CommandText = sql;
            command.Parameters.AddRange(parameters);
        }

        private void CreatePagedReadCommand(DbCommand command, int pageSize, int pageNumber)
        {
            var parameters = new[] { CreateSqlParameter("@PageNumber", pageNumber), CreateSqlParameter("@PageSize", pageSize) };

            var sql = string.Format(
                "SELECT * FROM {0} AS TBL WHERE `CreatedID` BETWEEN ((@PageNumber-1)*@PageSize+1) AND (@PageNumber*@PageSize) ORDER BY Timestamp DESC",
                _configuration.OutBoxTableName);

            command.CommandText = sql;
            AddParamtersParamArrayToCollection(parameters, command);
        }

        private void CreateBulkReadCommand(DbCommand command, IEnumerable<Guid> messageIds)
        {
            var inClause = GenerateInClauseAndAddParameters(command, messageIds.ToList());

            var sql = $"SELECT * FROM {_configuration.OutBoxTableName} WHERE `MessageID` IN ( {inClause} )";

            command.CommandText = sql;
        }

        private void CreatePagedOutstandingCommand(DbCommand command, double milliSecondsSinceAdded, int pageSize, int pageNumber)
        {
            var offset = (pageNumber - 1) * pageSize;
            var pagingSqlFormat =
                "SELECT * FROM {0} WHERE DISPATCHED IS NULL AND Timestamp < DATE_ADD(UTC_TIMESTAMP(), INTERVAL -@OutStandingSince SECOND) ORDER BY Timestamp DESC LIMIT @PageSize OFFSET @OffsetValue";
            var seconds = TimeSpan.FromMilliseconds(milliSecondsSinceAdded).Seconds > 0 ? TimeSpan.FromMilliseconds(milliSecondsSinceAdded).Seconds : 1;
            var parameters = new[]
            {
                CreateSqlParameter("@OffsetValue", offset), CreateSqlParameter("@PageSize", pageSize), CreateSqlParameter("@OutstandingSince", seconds)
            };

            var sql = string.Format(pagingSqlFormat, _configuration.OutBoxTableName);

            command.CommandText = sql;
            command.Parameters.AddRange(parameters);
        }

        private T ExecuteCommand<T>(Func<DbCommand, T> execute, string sql, int outboxTimeout, params MySqlParameter[] parameters)
        {
            var connection = _connectionProvider.GetConnection();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                AddParamtersParamArrayToCollection(parameters, command);

                if (outboxTimeout != -1) command.CommandTimeout = outboxTimeout;

                if (connection.State != ConnectionState.Open) connection.Open();
                var item = execute(command);
                
                if(!_connectionProvider.IsSharedConnection) connection.Dispose();
                else if (!_connectionProvider.HasOpenTransaction) connection.Close();
                
                return item;
            }
        }

        private async Task<T> ExecuteCommandAsync<T>(
            Func<DbCommand, Task<T>> execute,
            string sql,
            int timeoutInMilliseconds,
            CancellationToken cancellationToken = default(CancellationToken),
            params MySqlParameter[] parameters)
        {
            var connection = await _connectionProvider.GetConnectionAsync();
            using (var command = connection.CreateCommand())
            {
                if (timeoutInMilliseconds != -1) command.CommandTimeout = timeoutInMilliseconds;
                command.CommandText = sql;
                AddParamtersParamArrayToCollection(parameters, command);

                if (connection.State != ConnectionState.Open) await connection.OpenAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
                var item = await execute(command).ConfigureAwait(ContinueOnCapturedContext);
                
                if(!_connectionProvider.IsSharedConnection) connection.Dispose();
                else if (!_connectionProvider.HasOpenTransaction) await connection.CloseAsync();
                
                return item;
            }
        }

        private string GetAddSql()
        {
            var sql =
                string.Format(
                    "INSERT INTO {0} (MessageId, MessageType, Topic, Timestamp, CorrelationId, ReplyTo, ContentType, HeaderBag, Body) VALUES (@MessageId, @MessageType, @Topic, @Timestamp, @CorrelationId, @ReplyTo, @ContentType, @HeaderBag, @Body)",
                    _configuration.OutBoxTableName);
            return sql;
        }

        private (string sql, MySqlParameter[] parameters) GetBulkAddSql(List<Message> messages)
        {
            var messageParams = new List<string>();
            var parameters = new List<MySqlParameter>();
            
            for (int i = 0; i < messages.Count(); i++)
            {
                messageParams.Add($"(@p{i}_MessageId, @p{i}_MessageType, @p{i}_Topic, @p{i}_Timestamp, @p{i}_CorrelationId, @p{i}_ReplyTo, @p{i}_ContentType, @p{i}_HeaderBag, @p{i}_Body)");
                parameters.AddRange(InitAddDbParameters(messages[i], i));
                
            }
            var sql = $"INSERT INTO {_configuration.OutBoxTableName} (MessageId, MessageType, Topic, Timestamp, CorrelationId, ReplyTo, ContentType, HeaderBag, Body) VALUES {string.Join(",", messageParams)}";
            
            return (sql, parameters.ToArray());
        }

        private MySqlParameter[] InitAddDbParameters(Message message, int? position = null)
        {
            var prefix = position.HasValue ? $"p{position}_" : "";
            var bagJson = JsonSerializer.Serialize(message.Header.Bag, JsonSerialisationOptions.Options);
            return new[]
            {
                new MySqlParameter { ParameterName = $"@{prefix}MessageId", DbType = DbType.String, Value = message.Id.ToString() },
                new MySqlParameter { ParameterName = $"@{prefix}MessageType", DbType = DbType.String, Value = message.Header.MessageType.ToString() },
                new MySqlParameter { ParameterName = $"@{prefix}Topic", DbType = DbType.String, Value = message.Header.Topic, },
                new MySqlParameter { ParameterName = $"@{prefix}Timestamp", DbType = DbType.DateTime2, Value = message.Header.TimeStamp.ToUniversalTime() }, //always store in UTC, as this is how we query messages
                new MySqlParameter { ParameterName = $"@{prefix}CorrelationId", DbType = DbType.String, Value = message.Header.CorrelationId.ToString() },
                new MySqlParameter { ParameterName = $"@{prefix}ReplyTo", DbType = DbType.String, Value = message.Header.ReplyTo },
                new MySqlParameter { ParameterName = $"@{prefix}ContentType", DbType = DbType.String, Value = message.Header.ContentType },
                new MySqlParameter { ParameterName = $"@{prefix}HeaderBag", DbType = DbType.String, Value = bagJson },
                new MySqlParameter { ParameterName = $"@{prefix}Body", DbType = DbType.String, Value = message.Body.Value }
            };
        }

        private DbCommand InitMarkDispatchedCommand(DbConnection connection, IEnumerable<Guid> messageIds, DateTime? dispatchedAt)
        {
            var command = connection.CreateCommand();
            var inClause = GenerateInClauseAndAddParameters(command, messageIds.ToList());
            var sql = $"UPDATE {_configuration.OutBoxTableName} SET Dispatched = @DispatchedAt WHERE MessageId IN ( {inClause} )";

            command.CommandText = sql;
            command.Parameters.Add(CreateSqlParameter("@DispatchedAt", dispatchedAt?.ToUniversalTime())); //always store in UTC, as this is how we query messages
            return command;
        }

        private string GenerateInClauseAndAddParameters(DbCommand command, List<Guid> messageIds)
        {
            var paramNames = messageIds.Select((s, i) => "@p" + i).ToArray();

            for (int i = 0; i < paramNames.Count(); i++)
            {
                command.Parameters.Add(CreateSqlParameter(paramNames[i], messageIds[i]));
            }

            return string.Join(",", paramNames);
        }

        private static bool IsExceptionUnqiueOrDuplicateIssue(MySqlException sqlException)
        {
            return sqlException.Number == MySqlDuplicateKeyError;
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
            return dr.GetGuid(0);
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
            if (dr.Read())
            {
                return MapAMessage(dr);
            }

            return new Message();
        }
    }
}
