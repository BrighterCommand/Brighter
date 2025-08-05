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

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.Extensions.Logging;
using Npgsql;
using Paramore.Brighter.Logging;
using Paramore.Brighter.PostgreSql;

namespace Paramore.Brighter.Outbox.PostgreSql
{
    public class PostgreSqlOutboxSync : PostgreSqlOutboxBase, IAmABulkOutboxSync<Message>
    {
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<PostgreSqlOutboxSync>();
        
        public bool ContinueOnCapturedContext
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        /// <summary>
        /// Initialises a new instance of <see cref="PostgreSqlOutboxSync"> class.
        /// </summary>
        /// <param name="configuration">PostgreSql Outbox Configuration.</param>
        /// <param name="connectionProvider">The connection provider for PostgreSQL</param>
        public PostgreSqlOutboxSync(PostgreSqlOutboxConfiguration configuration, IPostgreSqlConnectionProvider connectionProvider = null)
            : base(configuration, connectionProvider)
        {
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
                    if (connectionProvider.HasOpenTransaction)
                        command.Transaction = connectionProvider.GetTransaction();
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
        /// Awaitable add the specified message.
        /// </summary>
        /// <param name="messages">The message.</param>
        /// <param name="outBoxTimeout">The time allowed for the write in milliseconds; on a -1 default</param>
        /// <param name="transactionConnectionProvider">The Connection Provider to use for this call</param>
        public void Add(IEnumerable<Message> messages, int outBoxTimeout = -1,
            IAmABoxTransactionConnectionProvider transactionConnectionProvider = null)
        {
            var connectionProvider = GetConnectionProvider(transactionConnectionProvider);
            var connection = GetOpenConnection(connectionProvider);

            try
            {
                using (var command = InitBulkAddDbCommand(connection, messages.ToList()))
                {
                    if (connectionProvider.HasOpenTransaction)
                        command.Transaction = connectionProvider.GetTransaction();
                    command.ExecuteNonQuery();
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
            var sql = string.Format(PostgreSqlOutboxQueries.GetMessageByIdCommand, _configuration.OutboxTableName);
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

        public void Delete(params Guid[] messageIds)
        {
            WriteToStore(null, connection => InitDeleteDispatchedCommand(connection, messageIds), null);
        }
        
        private void WriteToStore(IAmABoxTransactionConnectionProvider transactionConnectionProvider, Func<NpgsqlConnection, NpgsqlCommand> commandFunc, Action loggingAction)
        {
            var connectionProvider = _connectionProvider;
            if (transactionConnectionProvider != null && transactionConnectionProvider is IPostgreSqlConnectionProvider provider)
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
                        connection.Dispose();
                    else if (!connectionProvider.HasOpenTransaction)
                        connection.Close();
                }
            }
        }

        private NpgsqlConnection GetOpenConnection(IPostgreSqlConnectionProvider connectionProvider)
        {
            NpgsqlConnection connection = connectionProvider.GetConnection();

            if (connection.State != ConnectionState.Open)
                connection.Open();

            return connection;
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

        private Message MapFunction(IDataReader reader)
        {
            if (reader.Read())
            {
                return MapAMessage(reader);
            }

            return new Message();
        }
    }
}
