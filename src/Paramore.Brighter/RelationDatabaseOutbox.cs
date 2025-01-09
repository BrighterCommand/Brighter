using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter
{
    public abstract class RelationDatabaseOutbox(
        string outboxTableName,
        IRelationDatabaseOutboxQueries queries,
        ILogger logger)
        : IAmAnOutboxSync<Message, DbTransaction>, IAmAnOutboxAsync<Message, DbTransaction>
    {
        /// <summary>
        ///     If false we the default thread synchronization context to run any continuation, if true we re-use the original
        ///     synchronization context.
        ///     Default to false unless you know that you need true, as you risk deadlocks with the originating thread if you Wait
        ///     or access the Result or otherwise block. You may need the originating synchronization context if you need to access
        ///     thread specific storage such as HTTPContext
        /// </summary>
        public bool ContinueOnCapturedContext { get; set; }

        /// <summary>
        /// The Tracer that we want to use to capture telemetry
        /// We inject this so that we can use the same tracer as the calling application
        /// You do not need to set this property as we will set it when setting up the External Service Bus
        /// </summary>
        public IAmABrighterTracer? Tracer { private get; set; }

        /// <summary>
        ///     Adds the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>        
        /// <param name="outBoxTimeout"></param>
        /// <param name="transactionProvider">Connection Provider to use for this call</param>
        /// <returns>Task.</returns>
        public void Add(
            Message message,
            RequestContext requestContext,
            int outBoxTimeout = -1,
            IAmABoxTransactionProvider<DbTransaction>? transactionProvider = null)
        {
            var parameters = InitAddDbParameters(message);
            WriteToStore(transactionProvider, connection => InitAddDbCommand(connection, parameters), () =>
            {
                logger.LogWarning(
                    "MsSqlOutbox: A duplicate Message with the MessageId {Id} was inserted into the Outbox, ignoring and continuing",
                    message.Id);
            });
        }

        /// <summary>
        /// Adds the specified message.
        /// </summary>
        /// <param name="messages">The message.</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>        
        /// <param name="outBoxTimeout">How long to wait for the message before timing out</param>
        /// <param name="transactionProvider">Connection Provider to use for this call</param>
        /// <returns>Task.</returns>
        public void Add(
            IEnumerable<Message> messages,
            RequestContext? requestContext,
            int outBoxTimeout = -1,
            IAmABoxTransactionProvider<DbTransaction>? transactionProvider = null
        )
        {
            WriteToStore(transactionProvider,
                connection => InitBulkAddDbCommand(messages.ToList(), connection),
                () => logger.LogWarning("MsSqlOutbox: At least one message already exists in the outbox"));
        }

        /// <summary>
        /// Adds the specified message to the outbox 
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>        
        /// <param name="outBoxTimeout">How long to wait for the message before timing out</param>
        /// <param name="transactionProvider">Connection Provider to use for this call</param>
        /// <param name="cancellationToken">Cancellation Token</param>
        /// <returns>Task&lt;Message&gt;.</returns>
        public Task AddAsync(
            Message message,
            RequestContext requestContext,
            int outBoxTimeout = -1,
            IAmABoxTransactionProvider<DbTransaction>? transactionProvider = null,
            CancellationToken cancellationToken = default)
        {
            var parameters = InitAddDbParameters(message);
            return WriteToStoreAsync(transactionProvider,
                connection => InitAddDbCommand(connection, parameters), () =>
                {
                    logger.LogWarning(
                        "MsSqlOutbox: A duplicate Message with the MessageId {Id} was inserted into the Outbox, ignoring and continuing",
                        message.Id);
                },
                cancellationToken);
        }

        /// <summary>
        /// Awaitable add the specified message.
        /// </summary>
        /// <param name="messages">The message.</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>
        /// <param name="outBoxTimeout">The time allowed for the write in milliseconds; on a -1 default</param>
        /// <param name="transactionProvider">The Connection Provider to use for this call</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        /// <returns><see cref="Task"/>.</returns>
        public Task AddAsync(
            IEnumerable<Message> messages,
            RequestContext? requestContext,
            int outBoxTimeout = -1,
            IAmABoxTransactionProvider<DbTransaction>? transactionProvider = null,
            CancellationToken cancellationToken = default)
        {
            return WriteToStoreAsync(transactionProvider,
                connection => InitBulkAddDbCommand(messages.ToList(), connection),
                () => logger.LogWarning("MsSqlOutbox: At least one message already exists in the outbox"),
                cancellationToken);
        }

        /// <summary>
        /// Delete the specified messages
        /// </summary>
        /// <param name="messageIds">The id of the message to delete</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>        
        /// <param name="args">Additional parameters required for search, if any</param>
        public void Delete(string[] messageIds, RequestContext? requestContext, Dictionary<string, object>? args = null)
        {
            if (messageIds.Any())
                WriteToStore(null, connection => InitDeleteDispatchedCommand(connection, messageIds), null);
        }

        /// <summary>
        /// Delete the specified messages
        /// </summary>
        /// <param name="messageIds">The id of the message to delete</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>
        /// <param name="args">Additional parameters required for search, if any</param>
        /// <param name="cancellationToken">The Cancellation Token</param>
        public Task DeleteAsync(
            string[] messageIds,
            RequestContext? requestContext,
            Dictionary<string, object>? args = null,
            CancellationToken cancellationToken = default)
        {
            if (!messageIds.Any())
                return Task.CompletedTask;

            return WriteToStoreAsync(null, connection => InitDeleteDispatchedCommand(connection, messageIds), null,
                cancellationToken);
        }

        /// <summary>
        /// Retrieves messages that have been sent within the window
        /// </summary>
        /// <param name="dispatchedSince">How long ago would the message have been dispatched.</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>
        /// <param name="pageSize">How many messages in a page</param>
        /// <param name="pageNumber">Which page of messages to get</param>
        /// <param name="outboxTimeout"></param>
        /// <param name="args">Additional parameters required for search, if any</param>
        /// <param name="cancellationToken">The Cancellation Token</param>
        /// <returns>A list of dispatched messages</returns>
        public Task<IEnumerable<Message>> DispatchedMessagesAsync(
            TimeSpan dispatchedSince,
            RequestContext? requestContext,
            int pageSize = 100,
            int pageNumber = 1,
            int outboxTimeout = 0,
            Dictionary<string, object>? args = null,
            CancellationToken cancellationToken = default)
        {
            return ReadFromStoreAsync(
                connection =>
                    CreatePagedDispatchedCommand(connection, dispatchedSince, pageSize, pageNumber, outboxTimeout),
                dr => MapListFunctionAsync(dr, cancellationToken), cancellationToken);
        }

        /// <summary>
        /// Get the messages that have been dispatched
        /// </summary>
        /// <param name="hoursDispatchedSince">The number of hours since the message was dispatched</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>        
        /// <param name="pageSize">The amount to return</param>
        /// <param name="args">Additional parameters required for search, if any</param>
        /// <param name="cancellationToken">The Cancellation Token</param>
        /// <returns>Messages that have already been dispatched</returns>
        public Task<IEnumerable<Message>> DispatchedMessagesAsync(
            int hoursDispatchedSince,
            RequestContext? requestContext,
            int pageSize = 100,
            Dictionary<string, object>? args = null,
            CancellationToken cancellationToken = default)
        {
            return DispatchedMessagesAsync(TimeSpan.FromHours(hoursDispatchedSince),
                requestContext,
                pageSize,
                args: args, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Retrieves messages that have been sent within the window
        /// </summary>
        /// <param name="dispatchedSince">How long ago would the message have been dispatched in milliseconds</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>        
        /// <param name="pageSize">How many messages in a page</param>
        /// <param name="pageNumber">Which page of messages to get</param>
        /// <param name="outBoxTimeout"></param>
        /// <param name="args">Additional parameters required for search, if any</param>
        /// <returns>A list of dispatched messages</returns>
        public IEnumerable<Message> DispatchedMessages(
            TimeSpan dispatchedSince,
            RequestContext? requestContext,
            int pageSize = 100,
            int pageNumber = 1,
            int outBoxTimeout = 0,
            Dictionary<string, object>? args = null)
        {
            return ReadFromStore(
                connection =>
                    CreatePagedDispatchedCommand(connection, dispatchedSince, pageSize, pageNumber, outBoxTimeout),
                MapListFunction);
        }

        /// <summary>
        /// Retrieves messages that have been sent within the window
        /// </summary>
        /// <param name="hoursDispatchedSince">The number of hours since the message was dispatched</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>        
        /// <param name="pageSize">How many messages in a page</param>
        /// <param name="pageNumber">Which page of messages to get</param>
        /// <param name="outBoxTimeout">How long to wait for the message before timing out</param>
        /// <param name="args">Additional parameters required for search, if any</param>
        /// <returns>A list of dispatched messages</returns>
        public IEnumerable<Message> DispatchedMessages(
            int hoursDispatchedSince,
            RequestContext? requestContext,
            int pageSize = 100,
            int pageNumber = 1,
            int outBoxTimeout = 0,
            Dictionary<string, object>? args = null)
        {
            return ReadFromStore(
                connection =>
                    CreatePagedDispatchedCommand(connection, TimeSpan.FromHours(hoursDispatchedSince), pageSize,
                        pageNumber, outBoxTimeout),
                MapListFunction);
        }

        /// <summary>
        /// Gets the specified message
        /// </summary>
        /// <param name="messageIds">The Ids of the messages</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>        
        /// <param name="outBoxTimeout">How long to wait for the message before timing out</param>
        /// <param name="args">For outboxes that require additional parameters such as topic, provide an optional arg</param>
        /// <returns>The message</returns>
        public IEnumerable<Message> Get(IEnumerable<string> messageIds, RequestContext requestContext,
            int outBoxTimeout = -1,
            Dictionary<string, object>? args = null)
        {
            return ReadFromStore(connection => InitGetMessagesCommand(connection, messageIds.ToList(), outBoxTimeout),
                MapListFunction);
        }

        /// <summary>
        /// Gets the specified message
        /// </summary>
        /// <param name="messageId">The id of the message to get</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>        
        /// <param name="outBoxTimeout">How long to wait for the message before timing out</param>
        /// <param name="args">For outboxes that require additional parameters such as topic, provide an optional arg</param>
        /// <returns>The message</returns>
        public Message Get(string messageId, RequestContext requestContext, int outBoxTimeout = -1,
            Dictionary<string, object>? args = null)
        {
            var message = ReadFromStore(connection => InitGetMessageCommand(connection, messageId, outBoxTimeout),
                MapFunction);

            return message;
        }

        /// <summary>
        /// get as an asynchronous operation.
        /// </summary>
        /// <param name="messageId">The message identifier.</param>
        /// <param name="requestContext"></param>
        /// <param name="outBoxTimeout">The time allowed for the read in milliseconds; on  a -2 default</param>
        /// <param name="args">For outboxes that require additional parameters such as topic, provide an optional arg</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        /// <returns><see cref="Task{Message}" />.</returns>
        public async Task<Message> GetAsync(
            string messageId,
            RequestContext requestContext,
            int outBoxTimeout = -1,
            Dictionary<string, object>? args = null,
            CancellationToken cancellationToken = default)
        {
            var message = await ReadFromStoreAsync(
                connection => InitGetMessageCommand(connection, messageId, outBoxTimeout),
                dr => MapFunctionAsync(dr, cancellationToken), cancellationToken);

            return message;
        }

        /// <summary>
        /// Returns messages specified by the Ids
        /// </summary>
        /// <param name="messageIds">The Ids of the messages</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>        
        /// <param name="outBoxTimeout">The Timeout of the outbox.</param>
        /// <param name="cancellationToken">Cancellation Token.</param>
        /// <returns></returns>
        public Task<IEnumerable<Message>> GetAsync(
            IEnumerable<string> messageIds,
            RequestContext requestContext,
            int outBoxTimeout = -1,
            CancellationToken cancellationToken = default
        )
        {
            return ReadFromStoreAsync(
                connection => InitGetMessagesCommand(connection, messageIds.ToList(), outBoxTimeout),
                async (dr) => await MapListFunctionAsync(dr, cancellationToken), cancellationToken);
        }

        /// <summary>
        /// Returns all messages in the store
        /// </summary>
        /// <param name="pageSize">Number of messages to return in search results (default = 100)</param>
        /// <param name="pageNumber">Page number of results to return (default = 1)</param>
        /// <param name="args">Additional parameters required for search, if any</param>
        /// <returns>A list of messages</returns>
        public IList<Message> Get(int pageSize = 100, int pageNumber = 1, Dictionary<string, object>? args = null)
        {
            return ReadFromStore(connection => CreatePagedReadCommand(connection, pageSize, pageNumber),
                MapListFunction).ToList();
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
            Dictionary<string, object>? args = null,
            CancellationToken cancellationToken = default)
        {
            return (await ReadFromStoreAsync(connection => CreatePagedReadCommand(connection, pageSize, pageNumber),
                dr => MapListFunctionAsync(dr, cancellationToken), cancellationToken)).ToList();
        }

        /// <summary>
        /// Get the number of messages in the Outbox that are not dispatched
        /// </summary>
        /// <param name="cancellationToken">Cancel the async operation</param>
        /// <returns></returns>
        public Task<int> GetNumberOfOutstandingMessagesAsync(CancellationToken cancellationToken = default)
        {
            return ReadFromStoreAsync(CreateRemainingOutstandingCommand,
                dr => MapOutstandingCountAsync(dr, cancellationToken), cancellationToken);
        }

        /// <summary>
        /// Get the number of messages in the Outbox that are not dispatched
        /// </summary>
        /// <returns></returns>
        public int GetNumberOfOutstandingMessages()
        {
            return ReadFromStore(CreateRemainingOutstandingCommand, MapOutstandingCount);
        }


        /// <summary>
        /// Update a message to show it is dispatched
        /// </summary>
        /// <param name="id">The id of the message to update</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>
        /// <param name="dispatchedAt">When was the message dispatched, defaults to UTC now</param>
        /// <param name="args">Allows additional arguments for specific Outbox Db providers</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        public Task MarkDispatchedAsync(
            string id,
            RequestContext? requestContext,
            DateTimeOffset? dispatchedAt = null,
            Dictionary<string, object>? args = null,
            CancellationToken cancellationToken = default)
        {
            return WriteToStoreAsync(null,
                connection => InitMarkDispatchedCommand(connection, id, dispatchedAt ?? DateTimeOffset.UtcNow), null,
                cancellationToken);
        }

        /// <summary>
        /// Update messages to show it is dispatched
        /// </summary>
        /// <param name="ids">The ids of the messages to update</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>
        /// <param name="dispatchedAt">When was the message dispatched, defaults to UTC now</param>
        /// <param name="args">Allows additional arguments to be passed for specific Db providers</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        public Task MarkDispatchedAsync(
            IEnumerable<string> ids,
            RequestContext? requestContext,
            DateTimeOffset? dispatchedAt = null,
            Dictionary<string, object>? args = null,
            CancellationToken cancellationToken = default)
        {
            return WriteToStoreAsync(null,
                connection => InitMarkDispatchedCommand(connection, ids, dispatchedAt ?? DateTimeOffset.UtcNow), null,
                cancellationToken);
        }

        /// <summary>
        /// Update a message to show it is dispatched
        /// </summary>
        /// <param name="id">The id of the message to update</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>        
        /// <param name="dispatchedAt">When was the message dispatched, defaults to UTC now</param>
        /// <param name="args">Allows additional arguments to be provided for specific Outbox Db providers</param>
        public void MarkDispatched(string id, RequestContext requestContext, DateTimeOffset? dispatchedAt = null,
            Dictionary<string, object>? args = null)
        {
            WriteToStore(null, connection => InitMarkDispatchedCommand(connection, id, dispatchedAt ?? DateTime.UtcNow),
                null);
        }

        /// <summary>
        /// Messages still outstanding in the Outbox because their timestamp
        /// </summary>
        /// <param name="dispatchedSince">How many seconds since the message was sent do we wait to declare it outstanding</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>        
        /// <param name="pageSize">The number of entries on a page</param>
        /// <param name="pageNumber">The page to return</param>
        /// <param name="args">Additional parameters required for search, if any</param>
        /// <returns>Outstanding Messages</returns>
        public IEnumerable<Message> OutstandingMessages(
            TimeSpan dispatchedSince,
            RequestContext? requestContext,
            int pageSize = 100,
            int pageNumber = 1,
            Dictionary<string, object>? args = null)
        {
            return ReadFromStore(
                connection => CreatePagedOutstandingCommand(connection, dispatchedSince, pageSize, pageNumber, -1),
                MapListFunction);
        }

        /// <summary>
        /// Messages still outstanding in the Outbox because their timestamp
        /// </summary>
        /// <param name="dispatchedSince">How many seconds since the message was sent do we wait to declare it outstanding</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>
        /// <param name="pageSize">The number of entries to return in a page</param>
        /// <param name="pageNumber">The page number to return</param>
        /// <param name="args">Additional parameters required for search, if any</param>
        /// <param name="cancellationToken">Async Cancellation Token</param>
        /// <returns>Outstanding Messages</returns>
        public Task<IEnumerable<Message>> OutstandingMessagesAsync(
            TimeSpan dispatchedSince,
            RequestContext requestContext,
            int pageSize = 100,
            int pageNumber = 1,
            Dictionary<string, object>? args = null,
            CancellationToken cancellationToken = default)
        {
            return ReadFromStoreAsync(
                connection => CreatePagedOutstandingCommand(connection, dispatchedSince, pageSize, pageNumber, -1),
                dr => MapListFunctionAsync(dr, cancellationToken), cancellationToken);
        }

        protected abstract void WriteToStore(
            IAmABoxTransactionProvider<DbTransaction>? transactionProvider,
            Func<DbConnection, DbCommand> commandFunc,
            Action? loggingAction
        );

        protected abstract Task WriteToStoreAsync(
            IAmABoxTransactionProvider<DbTransaction>? transactionProvider,
            Func<DbConnection, DbCommand> commandFunc,
            Action? loggingAction,
            CancellationToken cancellationToken
        );

        protected abstract T ReadFromStore<T>(
            Func<DbConnection, DbCommand> commandFunc,
            Func<DbDataReader, T> resultFunc
        );

        protected abstract Task<T> ReadFromStoreAsync<T>(
            Func<DbConnection, DbCommand> commandFunc,
            Func<DbDataReader, Task<T>> resultFunc,
            CancellationToken cancellationToken
        );

        protected DbConnection GetOpenConnection(IAmARelationalDbConnectionProvider defaultConnectionProvider,
            IAmABoxTransactionProvider<DbTransaction>? transactionProvider)
        {
            var connectionProvider = defaultConnectionProvider;
            if (transactionProvider is IAmARelationalDbConnectionProvider transConnectionProvider)
                connectionProvider = transConnectionProvider;

            var connection = connectionProvider.GetConnection();

            if (connection.State != ConnectionState.Open)
                connection.Open();

            return connection;
        }

        protected void FinishWrite(DbConnection connection,
            IAmABoxTransactionProvider<DbTransaction>? transactionProvider)
        {
            if (transactionProvider != null)
                transactionProvider.Close();
            else
                connection.Close();
        }

        protected async Task<DbConnection> GetOpenConnectionAsync(
            IAmARelationalDbConnectionProvider defaultConnectionProvider,
            IAmABoxTransactionProvider<DbTransaction>? transactionProvider, CancellationToken cancellationToken)
        {
            var connectionProvider = defaultConnectionProvider;
            if (transactionProvider is IAmARelationalDbConnectionProvider transConnectionProvider)
                connectionProvider = transConnectionProvider;

            var connection = await connectionProvider.GetConnectionAsync(cancellationToken);

            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync(cancellationToken);

            return connection;
        }

        private DbCommand CreatePagedDispatchedCommand(
            DbConnection connection,
            TimeSpan timeDispatchedSince,
            int pageSize,
            int pageNumber,
            int outboxTimeout)
            => CreateCommand(connection, GenerateSqlText(queries.PagedDispatchedCommand), outboxTimeout,
                CreatePagedDispatchedParameters(timeDispatchedSince, pageSize, pageNumber));

        private DbCommand CreatePagedReadCommand(
            DbConnection connection,
            int pageSize,
            int pageNumber
        )
            => CreateCommand(connection, GenerateSqlText(queries.PagedReadCommand), 0,
                CreatePagedReadParameters(pageSize, pageNumber));

        private DbCommand CreatePagedOutstandingCommand(
            DbConnection connection,
            TimeSpan timeSinceAdded,
            int pageSize,
            int pageNumber,
            int outboxTimeout)
            => CreateCommand(connection, GenerateSqlText(queries.PagedOutstandingCommand), outboxTimeout,
                CreatePagedOutstandingParameters(timeSinceAdded, pageSize, pageNumber));

        private DbCommand CreateRemainingOutstandingCommand(DbConnection connection)
            => CreateCommand(connection, GenerateSqlText(queries.GetNumberOfOutstandingMessagesCommand), 0);

        private DbCommand InitAddDbCommand(
            DbConnection connection,
            IDbDataParameter[] parameters
        )
            => CreateCommand(connection, GenerateSqlText(queries.AddCommand), 0, parameters);

        private DbCommand InitBulkAddDbCommand(List<Message> messages, DbConnection connection)
        {
            var insertClause = GenerateBulkInsert(messages);
            return CreateCommand(connection, GenerateSqlText(queries.BulkAddCommand, insertClause.insertClause), 0,
                insertClause.parameters);
        }

        private DbCommand InitMarkDispatchedCommand(DbConnection connection, string messageId,
            DateTimeOffset? dispatchedAt)
            => CreateCommand(connection, GenerateSqlText(queries.MarkDispatchedCommand), 0,
                CreateSqlParameter("MessageId", messageId),
                CreateSqlParameter("DispatchedAt", dispatchedAt?.ToUniversalTime()));

        private DbCommand InitMarkDispatchedCommand(DbConnection connection, IEnumerable<string> messageIds,
            DateTimeOffset? dispatchedAt)
        {
            var inClause = GenerateInClauseAndAddParameters(messageIds.ToList());
            return CreateCommand(connection, GenerateSqlText(queries.MarkMultipleDispatchedCommand, inClause.inClause),
                0,
                inClause.parameters.Append(CreateSqlParameter("DispatchedAt", dispatchedAt?.ToUniversalTime()))
                    .ToArray());
        }

        private DbCommand InitGetMessageCommand(DbConnection connection, string messageId, int outBoxTimeout = -1)
            => CreateCommand(connection, GenerateSqlText(queries.GetMessageCommand), outBoxTimeout,
                CreateSqlParameter("MessageId", messageId));

        private DbCommand InitGetMessagesCommand(DbConnection connection, List<string> messageIds,
            int outBoxTimeout = -1)
        {
            var inClause = GenerateInClauseAndAddParameters(messageIds);
            return CreateCommand(connection, GenerateSqlText(queries.GetMessagesCommand, inClause.inClause),
                outBoxTimeout,
                inClause.parameters);
        }

        private string GenerateSqlText(string sqlFormat, params string[] orderedParams)
            => string.Format(sqlFormat, orderedParams.Prepend(outboxTableName).ToArray());

        private DbCommand InitDeleteDispatchedCommand(DbConnection connection, IEnumerable<string> messageIds)
        {
            var inClause = GenerateInClauseAndAddParameters(messageIds.ToList());
            return CreateCommand(connection, GenerateSqlText(queries.DeleteMessagesCommand, inClause.inClause), 0,
                inClause.parameters);
        }

        protected abstract DbCommand CreateCommand(DbConnection connection, string sqlText, int outBoxTimeout,
            params IDbDataParameter[] parameters);


        protected abstract IDbDataParameter[] CreatePagedOutstandingParameters(TimeSpan since, int pageSize,
            int pageNumber);

        protected abstract IDbDataParameter[] CreatePagedDispatchedParameters(TimeSpan dispatchedSince, int pageSize,
            int pageNumber);

        protected abstract IDbDataParameter[] CreatePagedReadParameters(int pageSize, int pageNumber);

        protected abstract IDbDataParameter CreateSqlParameter(string parameterName, object? value);
        protected abstract IDbDataParameter[] InitAddDbParameters(Message message, int? position = null);

        protected abstract Message MapFunction(DbDataReader dr);

        protected abstract Task<Message> MapFunctionAsync(DbDataReader dr, CancellationToken cancellationToken);

        protected abstract IEnumerable<Message> MapListFunction(DbDataReader dr);

        protected abstract Task<IEnumerable<Message>> MapListFunctionAsync(DbDataReader dr,
            CancellationToken cancellationToken);

        protected abstract Task<int> MapOutstandingCountAsync(DbDataReader dr, CancellationToken cancellationToken);
        protected abstract int MapOutstandingCount(DbDataReader dr);

        private (string inClause, IDbDataParameter[] parameters) GenerateInClauseAndAddParameters(
            List<string> messageIds)
        {
            var paramNames = messageIds.Select((s, i) => "@p" + i).ToArray();

            var parameters = new IDbDataParameter[messageIds.Count];
            for (int i = 0; i < paramNames.Count(); i++)
            {
                parameters[i] = CreateSqlParameter(paramNames[i], messageIds[i]);
            }

            return (string.Join(",", paramNames), parameters);
        }

        private (string insertClause, IDbDataParameter[] parameters) GenerateBulkInsert(List<Message> messages)
        {
            var messageParams = new List<string>();
            var parameters = new List<IDbDataParameter>();

            for (int i = 0; i < messages.Count(); i++)
            {
                messageParams.Add(
                    $"(@p{i}_MessageId, @p{i}_MessageType, @p{i}_Topic, @p{i}_Timestamp, @p{i}_CorrelationId, @p{i}_ReplyTo, @p{i}_ContentType, @p{i}_PartitionKey, @p{i}_HeaderBag, @p{i}_Body)");
                parameters.AddRange(InitAddDbParameters(messages[i], i));
            }

            return (string.Join(",", messageParams), parameters.ToArray());
        }
    }
}
