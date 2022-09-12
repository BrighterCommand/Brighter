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
using Microsoft.Data.SqlClient;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Paramore.Brighter.MsSql;

namespace Paramore.Brighter.Outbox.MsSql
{
    /// <summary>
    ///     Class MsSqlOutbox.
    /// </summary>
    public class MsSqlOutbox :
        IAmABulkOutboxSync<Message>, 
        IAmABulkOutboxAsync<Message>
    {
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<MsSqlOutbox>();

        private const int MsSqlDuplicateKeyError_UniqueIndexViolation = 2601;
        private const int MsSqlDuplicateKeyError_UniqueConstraintViolation = 2627;
        private readonly MsSqlConfiguration _configuration;
        private readonly IMsSqlConnectionProvider _connectionProvider;

        /// <summary>
        ///     If false we the default thread synchronization context to run any continuation, if true we re-use the original
        ///     synchronization context.
        ///     Default to false unless you know that you need true, as you risk deadlocks with the originating thread if you Wait
        ///     or access the Result or otherwise block. You may need the originating synchronization context if you need to access
        ///     thread specific storage such as HTTPContext
        /// </summary>
        public bool ContinueOnCapturedContext { get; set; }

        /// <summary>
        ///     Initializes a new instance of the <see cref="MsSqlOutbox" /> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="connectionProvider">The connection factory.</param>
        public MsSqlOutbox(MsSqlConfiguration configuration, IMsSqlConnectionProvider connectionProvider)
        {
            _configuration = configuration;
            ContinueOnCapturedContext = false;
            _connectionProvider = connectionProvider;
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="MsSqlOutbox" /> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        public MsSqlOutbox(MsSqlConfiguration configuration) : this(configuration, new MsSqlSqlAuthConnectionProvider(configuration))
        {
        }

        /// <summary>
        ///     Adds the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="outBoxTimeout"></param>
        /// <param name="transactionConnectionProvider">Connection Provider to use for this call</param>
        /// <returns>Task.</returns>
        public void Add(Message message, int outBoxTimeout = -1, IAmABoxTransactionConnectionProvider transactionConnectionProvider = null)
        {
            var parameters = InitAddDbParameters(message);
            
            var connectionProvider = _connectionProvider;
            if (transactionConnectionProvider != null && transactionConnectionProvider is IMsSqlTransactionConnectionProvider provider)
                connectionProvider = provider;
            
            var connection = connectionProvider.GetConnection();
            
            if(connection.State!= ConnectionState.Open) connection.Open();
            using (var command = InitAddDbCommand(connection, parameters))
            {
                try
                {
                    if (connectionProvider.HasOpenTransaction) command.Transaction = connectionProvider.GetTransaction(); 
                    command.ExecuteNonQuery();
                }
                catch (SqlException sqlException)
                {
                    if (sqlException.Number == MsSqlDuplicateKeyError_UniqueIndexViolation ||
                        sqlException.Number == MsSqlDuplicateKeyError_UniqueConstraintViolation)
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
        
        /// <summary>
        ///     Adds the specified message.
        /// </summary>
        /// <param name="messages">The message.</param>
        /// <param name="outBoxTimeout"></param>
        /// <param name="transactionConnectionProvider">Connection Provider to use for this call</param>
        /// <returns>Task.</returns>
        public void Add(IEnumerable<Message> messages, int outBoxTimeout = -1, IAmABoxTransactionConnectionProvider transactionConnectionProvider = null)
        {
            var connectionProvider = _connectionProvider;
            if (transactionConnectionProvider != null && transactionConnectionProvider is IMsSqlTransactionConnectionProvider provider)
                connectionProvider = provider;
            
            var connection = connectionProvider.GetConnection();
            
            if(connection.State!= ConnectionState.Open) connection.Open();
            using (var command = InitBulkAddDbCommand(messages.ToList(), connection))
            {
                try
                {
                    if (connectionProvider.HasOpenTransaction) command.Transaction = connectionProvider.GetTransaction(); 
                    command.ExecuteNonQuery();
                }
                catch (SqlException sqlException)
                {
                    if (sqlException.Number == MsSqlDuplicateKeyError_UniqueIndexViolation ||
                        sqlException.Number == MsSqlDuplicateKeyError_UniqueConstraintViolation)
                    {
                        s_logger.LogWarning(
                            "MsSqlOutbox: At least one message already exists in the outbox");
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
        ///     Gets the specified message identifier.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="outBoxTimeout"></param>
        /// <param name="cancellationToken">Cancellation Token</param>
        /// <param name="transactionConnectionProvider">Connection Provider to use for this call</param>
        /// <returns>Task&lt;Message&gt;.</returns>
        public async Task AddAsync(Message message, int outBoxTimeout = -1, CancellationToken cancellationToken = default(CancellationToken), IAmABoxTransactionConnectionProvider transactionConnectionProvider = null)
        {
            var parameters = InitAddDbParameters(message);
            
            var connectionProvider = _connectionProvider;
            if (transactionConnectionProvider != null && transactionConnectionProvider is IMsSqlConnectionProvider provider)
                connectionProvider = provider;

            var connection = await connectionProvider.GetConnectionAsync(cancellationToken);
            
            if(connection.State!= ConnectionState.Open) await connection.OpenAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
            using (var command = InitAddDbCommand(connection, parameters))
            {
                try
                {
                    if (connectionProvider.IsSharedConnection) command.Transaction = connectionProvider.GetTransaction();
                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
                }
                catch (SqlException sqlException)
                {
                    if (sqlException.Number == MsSqlDuplicateKeyError_UniqueIndexViolation ||
                        sqlException.Number == MsSqlDuplicateKeyError_UniqueConstraintViolation)
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
            if (transactionConnectionProvider != null && transactionConnectionProvider is IMsSqlConnectionProvider provider)
                connectionProvider = provider;
            
            var connection = await connectionProvider.GetConnectionAsync(cancellationToken);

            if(connection.State!= ConnectionState.Open) await connection.OpenAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
            using (var command = InitBulkAddDbCommand(messages.ToList(), connection))
            {
                try
                {
                    if (connectionProvider.IsSharedConnection) command.Transaction = connectionProvider.GetTransaction();
                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
                }
                catch (SqlException sqlException)
                {
                    if (sqlException.Number == MsSqlDuplicateKeyError_UniqueIndexViolation ||
                        sqlException.Number == MsSqlDuplicateKeyError_UniqueConstraintViolation)
                    {
                        s_logger.LogWarning(
                            "MsSqlOutbox: At least one message already exists in the outbox");
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
        /// Retrieves messages that have been sent within the window
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

                if(connection.State!= ConnectionState.Open) connection.Open();
                
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
        /// <param name="messageId">The id of the message to get</param>
        /// <param name="outBoxTimeout">How long to wait for the message before timing out</param>
        /// <returns>The message</returns>
        public Message Get(Guid messageId, int outBoxTimeout = -1)
        {
            var sql = $"SELECT * FROM {_configuration.OutBoxTableName} WHERE MessageId = @MessageId";
            var parameters = new[]
            {
                CreateSqlParameter("MessageId", messageId)
            };

            return ExecuteCommand(command => MapFunction(command.ExecuteReader()), sql, outBoxTimeout, parameters);
        }

        /// <summary>
        /// get as an asynchronous operation.
        /// </summary>
        /// <param name="messageId">The message identifier.</param>
        /// <param name="outBoxTimeout">The time allowed for the read in milliseconds; on  a -2 default</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        /// <returns><see cref="Task{Message}" />.</returns>
        public async Task<Message> GetAsync(Guid messageId, int outBoxTimeout = -1, CancellationToken cancellationToken = default(CancellationToken))
        {
            var sql = $"SELECT * FROM {_configuration.OutBoxTableName} WHERE MessageId = @MessageId";

            var parameters = new[]
            {
                CreateSqlParameter("MessageId", messageId)
            };

            return await ExecuteCommandAsync(
                async command => await MapFunctionAsync(await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext)),
                sql,
                outBoxTimeout,
                cancellationToken,
                parameters)
                .ConfigureAwait(ContinueOnCapturedContext);
        }

        /// <summary>
        /// Returns messages specified by the Ids
        /// </summary>
        /// <param name="outBoxTimeout">The Timeout of the outbox.</param>
        /// <param name="cancellationToken">Cancellation Token.</param>
        /// <param name="messageIds">The Ids of the messages</param>
        /// <returns></returns>
        public async Task<IEnumerable<Message>> GetAsync(IEnumerable<Guid> messageIds, int outBoxTimeout = -1,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var connection = await _connectionProvider.GetConnectionAsync(cancellationToken);
            using (var command = connection.CreateCommand())
            {
                CreateListOfMessagesCommand(command, messageIds.ToList());

                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
                
                var dbDataReader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);

                var messages = new List<Message>();
                while (await dbDataReader.ReadAsync(cancellationToken))
                {
                    messages.Add(MapAMessage(dbDataReader));
                }
                dbDataReader.Close();

                if (!_connectionProvider.IsSharedConnection)
                    connection.Dispose();
                else if (!_connectionProvider.HasOpenTransaction)
                    connection.Close();

                return messages;
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
            var connection = _connectionProvider.GetConnection();
            using (var command = connection.CreateCommand())
            {
                CreatePagedReadCommand(command, pageSize, pageNumber);

                if(connection.State != ConnectionState.Open) connection.Open();
                
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
        /// <param name="cancellationToken">Cancellation Token</param>
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

                if(connection.State!= ConnectionState.Open) await connection.OpenAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);

                if (_connectionProvider.HasOpenTransaction) command.Transaction = _connectionProvider.GetTransaction(); 
                var dbDataReader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);

                var messages = new List<Message>();
                while (await dbDataReader.ReadAsync(cancellationToken))
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
        /// Update a message to show it is dispatched
        /// </summary>
        /// <param name="id">The id of the message to update</param>
        /// <param name="dispatchedAt">When was the message dispatched, defaults to UTC now</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>

        public async Task MarkDispatchedAsync(Guid id, DateTime? dispatchedAt = null, Dictionary<string, object> args = null, CancellationToken cancellationToken = default)
        {
            var connection = await _connectionProvider.GetConnectionAsync(cancellationToken);
           
            if(connection.State!= ConnectionState.Open) await connection.OpenAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
            using (var command = InitMarkDispatchedCommand(connection, id, dispatchedAt ?? DateTime.UtcNow))
            {
                if (_connectionProvider.HasOpenTransaction) command.Transaction = _connectionProvider.GetTransaction();
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
            }
            if(!_connectionProvider.IsSharedConnection) connection.Dispose();
            else if (!_connectionProvider.HasOpenTransaction) connection.Close();
        }

        /// <summary>
        /// Update messages to show it is dispatched
        /// </summary>
        /// <param name="ids">The ids of the messages to update</param>
        /// <param name="dispatchedAt">When was the message dispatched, defaults to UTC now</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
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
                connection.Close();
        }

        /// <summary>
        /// Update a message to show it is dispatched
        /// </summary>
        /// <param name="id">The id of the message to update</param>
        /// <param name="dispatchedAt">When was the message dispatched, defaults to UTC now</param>
        public void MarkDispatched(Guid id, DateTime? dispatchedAt = null, Dictionary<string, object> args = null)
        {
            var connection = _connectionProvider.GetConnection();
            if(connection.State!= ConnectionState.Open) connection.Open();
            using (var command = InitMarkDispatchedCommand(connection, id, dispatchedAt ?? DateTime.UtcNow))
            {
                if (_connectionProvider.HasOpenTransaction) command.Transaction = _connectionProvider.GetTransaction();
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

                if(connection.State!= ConnectionState.Open) connection.Open();

                if (_connectionProvider.HasOpenTransaction) command.Transaction = _connectionProvider.GetTransaction(); 
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

               if(connection.State!= ConnectionState.Open) await connection.OpenAsync(cancellationToken);

               if (_connectionProvider.HasOpenTransaction) command.Transaction = _connectionProvider.GetTransaction(); 
               var dbDataReader = await command.ExecuteReaderAsync(cancellationToken);

               var messages = new List<Message>();
               while (await dbDataReader.ReadAsync(cancellationToken))
               {
                   messages.Add(MapAMessage(dbDataReader));
               }
               dbDataReader.Close();
                
               if(!_connectionProvider.IsSharedConnection) connection.Dispose();
               else if (!_connectionProvider.HasOpenTransaction) connection.Close();
                
               return messages;
           }
       }
        
        private void CreatePagedDispatchedCommand(SqlCommand command, double millisecondsDispatchedSince, int pageSize, int pageNumber)
        {
            var pagingSqlFormat = "SELECT * FROM (SELECT ROW_NUMBER() OVER(ORDER BY Timestamp DESC) AS NUMBER, * FROM {0}) AS TBL WHERE DISPATCHED IS NOT NULL AND DISPATCHED < DATEADD(millisecond, @OutStandingSince, getutcdate()) AND NUMBER BETWEEN ((@PageNumber-1)*@PageSize+1) AND (@PageNumber*@PageSize) ORDER BY Timestamp DESC";
            var parameters = new[]
            {
                CreateSqlParameter("PageNumber", pageNumber),
                CreateSqlParameter("PageSize", pageSize),
                CreateSqlParameter("OutstandingSince", -1 * millisecondsDispatchedSince)
            };

            var sql = string.Format(pagingSqlFormat, _configuration.OutBoxTableName);

            command.CommandText = sql;
            command.Parameters.AddRange(parameters);
        }

        private void CreatePagedReadCommand(SqlCommand command, int pageSize, int pageNumber)
        {
            var pagingSqlFormat = "SELECT * FROM (SELECT ROW_NUMBER() OVER(ORDER BY Timestamp DESC) AS NUMBER, * FROM {0}) AS TBL WHERE NUMBER BETWEEN ((@PageNumber-1)*@PageSize+1) AND (@PageNumber*@PageSize) ORDER BY Timestamp DESC";
            var parameters = new[]
            {
                CreateSqlParameter("PageNumber", pageNumber),
                CreateSqlParameter("PageSize", pageSize)
            };

            var sql = string.Format(pagingSqlFormat, _configuration.OutBoxTableName);

            command.CommandText = sql;
            command.Parameters.AddRange(parameters);
        }
        
        private void CreatePagedOutstandingCommand(SqlCommand command, double milliSecondsSinceAdded, int pageSize, int pageNumber)
        {
            var pagingSqlFormat = "SELECT * FROM (SELECT ROW_NUMBER() OVER(ORDER BY Timestamp ASC) AS NUMBER, * FROM {0} WHERE DISPATCHED IS NULL) AS TBL WHERE TIMESTAMP < DATEADD(millisecond, -@OutStandingSince, getutcdate()) AND NUMBER BETWEEN ((@PageNumber-1)*@PageSize+1) AND (@PageNumber*@PageSize) ORDER BY Timestamp ASC";
            var parameters = new[]
            {
                CreateSqlParameter("PageNumber", pageNumber),
                CreateSqlParameter("PageSize", pageSize),
                CreateSqlParameter("OutstandingSince", milliSecondsSinceAdded)
            };

            var sql = string.Format(pagingSqlFormat, _configuration.OutBoxTableName);

            command.CommandText = sql;
            command.Parameters.AddRange(parameters);
        }

        private void CreateListOfMessagesCommand(SqlCommand command, List<Guid> messageIds)
        {
            var inClause = GenerateInClauseAndAddParameters(command, messageIds.ToList());
            var sql = $"SELECT * FROM {_configuration.OutBoxTableName} WHERE MessageId IN ( {inClause} )";

            command.CommandText = sql;
        }


       //Fold this code back in as there is only one choice
        private SqlParameter CreateSqlParameter(string parameterName, object value)
        {
            return new SqlParameter(parameterName, value ?? DBNull.Value);
        }

        private T ExecuteCommand<T>(Func<SqlCommand, T> execute, string sql, int outboxTimeout, params SqlParameter[] parameters)
        {
            var connection = _connectionProvider.GetConnection();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                command.Parameters.AddRange(parameters);

                if (outboxTimeout != -1) command.CommandTimeout = outboxTimeout;

                if(connection.State!= ConnectionState.Open) connection.Open();
                var response = execute(command);
                
                if(!_connectionProvider.IsSharedConnection) connection.Dispose();
                else if (!_connectionProvider.HasOpenTransaction) connection.Close();
                
                return response;
            }
        }

        private async Task<T> ExecuteCommandAsync<T>(
            Func<SqlCommand, Task<T>> execute,
            string sql,
            int timeoutInMilliseconds,
            CancellationToken cancellationToken = default(CancellationToken),
            params SqlParameter[] parameters)
        {
            var connection = await _connectionProvider.GetConnectionAsync(cancellationToken);
            using (var command = connection.CreateCommand())
            {
                if (timeoutInMilliseconds != -1) command.CommandTimeout = timeoutInMilliseconds;
                command.CommandText = sql;
                command.Parameters.AddRange(parameters);

                if(connection.State!= ConnectionState.Open) await connection.OpenAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
                var response =  await execute(command).ConfigureAwait(ContinueOnCapturedContext);
                
                if(!_connectionProvider.IsSharedConnection) connection.Dispose();
                else if (!_connectionProvider.HasOpenTransaction) connection.Close();
                
                return response;
            }
        }
        
        private SqlCommand InitAddDbCommand(SqlConnection connection, SqlParameter[] parameters)
        {
            var command = connection.CreateCommand();
            var sql = $"INSERT INTO {_configuration.OutBoxTableName} (MessageId, MessageType, Topic, Timestamp, CorrelationId, ReplyTo, ContentType, HeaderBag, Body) VALUES (@MessageId, @MessageType, @Topic, @Timestamp, @CorrelationId, @ReplyTo, @ContentType, @HeaderBag, @Body)";
            command.CommandText = sql;
            command.Parameters.AddRange(parameters);
            return command;
        }

        private SqlCommand InitBulkAddDbCommand(List<Message> messages, SqlConnection connection)
        {
            var messageParams = new List<string>();
            var parameters = new List<SqlParameter>();
            
            for (int i = 0; i < messages.Count(); i++)
            {
                messageParams.Add($"(@p{i}_MessageId, @p{i}_MessageType, @p{i}_Topic, @p{i}_Timestamp, @p{i}_CorrelationId, @p{i}_ReplyTo, @p{i}_ContentType, @p{i}_HeaderBag, @p{i}_Body)");
                parameters.AddRange(InitAddDbParameters(messages[i], i));
                
            }
            var sql = $"INSERT INTO {_configuration.OutBoxTableName} (MessageId, MessageType, Topic, Timestamp, CorrelationId, ReplyTo, ContentType, HeaderBag, Body) VALUES {string.Join(",", messageParams)}";
            
            var command = connection.CreateCommand();
            
            command.CommandText = sql;
            command.Parameters.AddRange(parameters.ToArray());
            return command;
            
        }

        private SqlParameter[] InitAddDbParameters(Message message, int? position = null)
        {
            var prefix = position.HasValue ? $"p{position}_" : "";
            var bagJson = JsonSerializer.Serialize(message.Header.Bag, JsonSerialisationOptions.Options);
            var parameters = new[]
            {
                CreateSqlParameter($"{prefix}MessageId", message.Id),
                CreateSqlParameter($"{prefix}MessageType", message.Header.MessageType.ToString()),
                CreateSqlParameter($"{prefix}Topic", message.Header.Topic),
                CreateSqlParameter($"{prefix}Timestamp", message.Header.TimeStamp.ToUniversalTime()), //always store in UTC, as this is how we query messages
                CreateSqlParameter($"{prefix}CorrelationId", message.Header.CorrelationId),
                CreateSqlParameter($"{prefix}ReplyTo", message.Header.ReplyTo),
                CreateSqlParameter($"{prefix}ContentType", message.Header.ContentType),
                CreateSqlParameter($"{prefix}HeaderBag", bagJson),
                CreateSqlParameter($"{prefix}Body", message.Body.Value)
            };
            return parameters;
        }
        
        private SqlCommand InitMarkDispatchedCommand(SqlConnection connection, Guid messageId, DateTime? dispatchedAt)
        {
            var command = connection.CreateCommand();
            var sql = $"UPDATE {_configuration.OutBoxTableName} SET Dispatched = @DispatchedAt WHERE MessageId = @MessageId";
            command.CommandText = sql;
            command.Parameters.Add(CreateSqlParameter("MessageId", messageId));
            command.Parameters.Add(CreateSqlParameter("DispatchedAt", dispatchedAt?.ToUniversalTime())); //always store in UTC, as this is how we query messages
            return command;
         }
        private SqlCommand InitMarkDispatchedCommand(SqlConnection connection, IEnumerable<Guid> messageIds, DateTime? dispatchedAt)
        {
            var command = connection.CreateCommand();
            var inClause = GenerateInClauseAndAddParameters(command, messageIds.ToList());
            var sql = $"UPDATE {_configuration.OutBoxTableName} SET Dispatched = @DispatchedAt WHERE MessageId in ( {inClause} )";

            command.CommandText = sql;
            command.Parameters.Add(CreateSqlParameter("DispatchedAt", dispatchedAt?.ToUniversalTime())); //always store in UTC, as this is how we query messages

            return command;
        }

        private string GenerateInClauseAndAddParameters(SqlCommand command, List<Guid> messageIds)
        {
            var paramNames = messageIds.Select((s, i) => "@p" + i).ToArray();

            for (int i = 0; i < paramNames.Count(); i++)
            {
                command.Parameters.Add(CreateSqlParameter(paramNames[i], messageIds[i]));
            }

            return string.Join(",", paramNames);
        }

        private Message MapAMessage(SqlDataReader dr)
        {
            var id = GetMessageId(dr);
            var messageType = GetMessageType(dr);
            var topic = GetTopic(dr);

            var header = new MessageHeader(id, topic, messageType);
            

            //new schema....we've got the extra header information
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

        private static string GetTopic(SqlDataReader dr)
        {
            return dr.GetString(dr.GetOrdinal("Topic"));
        }

        private static MessageType GetMessageType(SqlDataReader dr)
        {
            return (MessageType) Enum.Parse(typeof (MessageType), dr.GetString(dr.GetOrdinal("MessageType")));
        }

        private static Guid GetMessageId(SqlDataReader dr)
        {
            return dr.GetGuid(dr.GetOrdinal("MessageId"));
        }

        private string GetContentType(SqlDataReader dr)
        {
            var ordinal = dr.GetOrdinal("ContentType");
            if (dr.IsDBNull(ordinal)) return null; 
            
            var replyTo = dr.GetString(ordinal);
            return replyTo;
        }

        private string GetReplyTo(SqlDataReader dr)
        {
             var ordinal = dr.GetOrdinal("ReplyTo");
             if (dr.IsDBNull(ordinal)) return null; 
             
             var replyTo = dr.GetString(ordinal);
             return replyTo;
        }

        private static Dictionary<string, object> GetContextBag(SqlDataReader dr)
        {
            var i = dr.GetOrdinal("HeaderBag");
            var headerBag = dr.IsDBNull(i) ? "" : dr.GetString(i);
            var dictionaryBag = JsonSerializer.Deserialize<Dictionary<string, object>>(headerBag, JsonSerialisationOptions.Options);
            return dictionaryBag;
        }

        private Guid? GetCorrelationId(SqlDataReader dr)
        {
            var ordinal = dr.GetOrdinal("CorrelationId");
            if (dr.IsDBNull(ordinal)) return null; 
            
            var correlationId = dr.GetGuid(ordinal);
            return correlationId;
        }

        private static DateTime GetTimeStamp(SqlDataReader dr)
        {
            var ordinal = dr.GetOrdinal("Timestamp");
            var timeStamp = dr.IsDBNull(ordinal)
                ? DateTime.MinValue
                : dr.GetDateTime(ordinal);
            return timeStamp;
        }

        private Message MapFunction(SqlDataReader dr)
        {
            Message message = null;
            if (dr.Read())
            {
                message = MapAMessage(dr);
            }
            dr.Close();

            return message ?? new Message();
        }
        
        private async Task<Message> MapFunctionAsync(SqlDataReader dr)
        {
            Message message = null;
            if (await dr.ReadAsync())
            {
                message = MapAMessage(dr);
            }
            dr.Close();

            return message ?? new Message();
        }
    }
}
