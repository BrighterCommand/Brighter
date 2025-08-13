using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter
{
    public abstract partial class RelationDatabaseOutbox(
        DbSystem dbSystem,
        IAmARelationalDatabaseConfiguration configuration,
        IAmARelationalDbConnectionProvider connectionProvider,
        IRelationDatabaseOutboxQueries queries,
        ILogger logger,
        InstrumentationOptions instrumentationOptions = InstrumentationOptions.All)
        : IAmAnOutboxSync<Message, DbTransaction>, IAmAnOutboxAsync<Message, DbTransaction>
    {

        protected IAmARelationalDatabaseConfiguration DatabaseConfiguration { get; } = configuration;

        protected IAmARelationalDbConnectionProvider ConnectionProvider { get; } = connectionProvider;

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
            var dbAttributes = new Dictionary<string, string>
            {
                { "db.operation.parameter.message.id", message.Id.Value },
                { "db.operation.name", ExtractSqlOperationName(queries.AddCommand) },
                { "db.query.text", queries.AddCommand }
            };
            
            var span = Tracer?.CreateDbSpan(
                new BoxSpanInfo(dbSystem, DatabaseConfiguration.DatabaseName, BoxDbOperation.Add, DatabaseConfiguration.OutBoxTableName,
                    dbAttributes: dbAttributes),
                requestContext?.Span,
                options: instrumentationOptions);

            try
            {
                var parameters = InitAddDbParameters(message);
                WriteToStore(transactionProvider, connection => InitAddDbCommand(connection, parameters), () =>
                {
                    logger.LogWarning(
                        "MsSqlOutbox: A duplicate Message with the MessageId {Id} was inserted into the Outbox, ignoring and continuing",
                        message.Id);
                });
            }
            finally
            {
                Tracer?.EndSpan(span);
            }
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
            var dbAttributes = new Dictionary<string, string>
            {
                { "db.operation.parameter.message.ids", string.Join(",", messages.Select(m => m.Id)) },
                { "db.operation.name", ExtractSqlOperationName(queries.BulkAddCommand) },
                { "db.query.text", queries.BulkAddCommand }
            };
            
            var span = Tracer?.CreateDbSpan(
                new BoxSpanInfo(dbSystem, DatabaseConfiguration.DatabaseName, BoxDbOperation.Add, DatabaseConfiguration.OutBoxTableName,
                    dbAttributes: dbAttributes),
                requestContext?.Span,
                options: instrumentationOptions);

            try
            {
                WriteToStore(transactionProvider,
                    connection => InitBulkAddDbCommand(messages.ToList(), connection),
                    () => logger.LogWarning("Outbox: At least one message already exists in the outbox"));
            }
            finally
            {
                Tracer?.EndSpan(span);
            }
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
            var dbAttributes = new Dictionary<string, string>
            {
                { "db.operation.parameter.message.id", message.Id.Value },
                { "db.operation.name", ExtractSqlOperationName(queries.AddCommand) },
                { "db.query.text", queries.AddCommand }
            };
            
            var span = Tracer?.CreateDbSpan(
                new BoxSpanInfo(dbSystem, DatabaseConfiguration.DatabaseName, BoxDbOperation.Add, DatabaseConfiguration.OutBoxTableName,
                    dbAttributes: dbAttributes),
                requestContext?.Span,
                options: instrumentationOptions);

            try
            {
                var parameters = InitAddDbParameters(message);
                return WriteToStoreAsync(transactionProvider,
                    connection => InitAddDbCommand(connection, parameters), () =>
                    {
                        logger.LogWarning(
                            "A duplicate Message with the MessageId {Id} was inserted into the Outbox, ignoring and continuing",
                            message.Id);
                    },
                    cancellationToken);
            }
            finally
            {
                Tracer?.EndSpan(span);
            }
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
            var dbAttributes = new Dictionary<string, string>()
            {
                { "db.operation.parameter.message.ids", string.Join(",", messages.Select(m => m.Id.Value)) },
                { "db.operation.name", ExtractSqlOperationName(queries.BulkAddCommand) },
                { "db.query.text", queries.BulkAddCommand }
            };
            var span = Tracer?.CreateDbSpan(
                new BoxSpanInfo(dbSystem, DatabaseConfiguration.DatabaseName, BoxDbOperation.Add, DatabaseConfiguration.OutBoxTableName,
                    dbAttributes: dbAttributes),
                requestContext?.Span,
                options: instrumentationOptions);

            try
            {
                return WriteToStoreAsync(transactionProvider,
                    connection => InitBulkAddDbCommand(messages.ToList(), connection),
                    () => logger.LogWarning("MsSqlOutbox: At least one message already exists in the outbox"),
                    cancellationToken);
            }
            finally
            {
                Tracer?.EndSpan(span);
            }
        }

        /// <summary>
        /// Delete the specified messages
        /// </summary>
        /// <param name="messageIds">The id of the message to delete</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>        
        /// <param name="args">Additional parameters required for search, if any</param>
        public void Delete(Id[] messageIds, RequestContext? requestContext, Dictionary<string, object>? args = null)
        {
            var dbAttributes = new Dictionary<string, string>()
            {
                { "db.operation.parameter.message.ids", string.Join(",", messageIds.Select(id => id.Value)) },
                { "db.operation.name", ExtractSqlOperationName(queries.DeleteMessagesCommand) },
                { "db.query.text", queries.DeleteMessagesCommand }
            };
            var span = Tracer?.CreateDbSpan(
                new BoxSpanInfo(dbSystem, DatabaseConfiguration.DatabaseName, BoxDbOperation.Delete, DatabaseConfiguration.OutBoxTableName,
                    dbAttributes: dbAttributes),
                requestContext?.Span,
                options: instrumentationOptions);

            try
            {
                if (messageIds.Any())
                    WriteToStore(null,
                        connection => InitDeleteDispatchedCommand(connection, messageIds.Select(m => m.ToString())),
                        null);
            }
            finally
            {
                Tracer?.EndSpan(span);
            }
        }

        /// <summary>
        /// Delete the specified messages
        /// </summary>
        /// <param name="messageIds">The id of the message to delete</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>
        /// <param name="args">Additional parameters required for search, if any</param>
        /// <param name="cancellationToken">The Cancellation Token</param>
        public Task DeleteAsync(
            Id[] messageIds,
            RequestContext? requestContext,
            Dictionary<string, object>? args = null,
            CancellationToken cancellationToken = default)
        {
            var dbAttributes = new Dictionary<string, string>()
            {
                { "db.operation.parameter.message.ids", string.Join(",", messageIds.Select(id => id.Value)) },
                { "db.operation.name", ExtractSqlOperationName(queries.DeleteMessagesCommand) },
                { "db.query.text", queries.DeleteMessagesCommand },
            };

            var span = Tracer?.CreateDbSpan(
                new BoxSpanInfo(dbSystem, DatabaseConfiguration.DatabaseName, BoxDbOperation.Delete, DatabaseConfiguration.OutBoxTableName,
                    dbAttributes: dbAttributes),
                requestContext?.Span,
                options: instrumentationOptions);

            try
            {
                if (!messageIds.Any())
                    return Task.CompletedTask;

                return WriteToStoreAsync(null,
                    connection => InitDeleteDispatchedCommand(connection, messageIds.Select(m => m.ToString())), null,
                    cancellationToken);
            }
            finally
            {
                Tracer?.EndSpan(span);
            }
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
        public async Task<IEnumerable<Message>> DispatchedMessagesAsync(
            TimeSpan dispatchedSince,
            RequestContext? requestContext,
            int pageSize = 100,
            int pageNumber = 1,
            int outboxTimeout = 0,
            Dictionary<string, object>? args = null,
            CancellationToken cancellationToken = default)
        {
            var dbAttributes = new Dictionary<string, string>()
            {
                { "db.operation.name", ExtractSqlOperationName(queries.PagedDispatchedCommand) },
                { "db.query.text", queries.PagedDispatchedCommand }
            };
            var span = Tracer?.CreateDbSpan(
                new BoxSpanInfo(dbSystem, DatabaseConfiguration.DatabaseName, BoxDbOperation.DispatchedMessages, DatabaseConfiguration.OutBoxTableName,
                    dbAttributes: dbAttributes),
                requestContext?.Span,
                options: instrumentationOptions);

            try
            {
                var result = await ReadFromStoreAsync(
                    connection =>
                        CreatePagedDispatchedCommand(connection, dispatchedSince, pageSize, pageNumber, outboxTimeout),
                    dr => MapListFunctionAsync(dr, cancellationToken), cancellationToken);

                span?.AddTag("db.response.returned_rows", result.Count());
                return result;
            }
            finally
            {
                Tracer?.EndSpan(span);
            }
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
            var dbAttributes = new Dictionary<string, string>()
            {
                { "db.operation.name", ExtractSqlOperationName(queries.PagedDispatchedCommand) },
                { "db.query.text", queries.PagedDispatchedCommand }
            };
            var span = Tracer?.CreateDbSpan(
                new BoxSpanInfo(dbSystem, DatabaseConfiguration.DatabaseName, BoxDbOperation.DispatchedMessages, DatabaseConfiguration.OutBoxTableName,
                    dbAttributes: dbAttributes),
                requestContext?.Span,
                options: instrumentationOptions);

            try
            {
                var result = ReadFromStore(
                    connection =>
                        CreatePagedDispatchedCommand(connection, dispatchedSince, pageSize, pageNumber, outBoxTimeout),
                    MapListFunction);

                span?.AddTag("db.response.returned_rows", result.Count());
                return result;
            }
            finally
            {
                Tracer?.EndSpan(span);
            }
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
            var dbAttributes = new Dictionary<string, string>()
            {
                { "db.operation.name", ExtractSqlOperationName(queries.PagedDispatchedCommand) },
                { "db.query.text", queries.PagedDispatchedCommand }
            };
            var span = Tracer?.CreateDbSpan(
                new BoxSpanInfo(dbSystem, DatabaseConfiguration.DatabaseName, BoxDbOperation.DispatchedMessages, DatabaseConfiguration.OutBoxTableName,
                    dbAttributes: dbAttributes),
                requestContext?.Span,
                options: instrumentationOptions);

            try
            {
                var result = ReadFromStore(
                    connection =>
                        CreatePagedDispatchedCommand(connection, TimeSpan.FromHours(hoursDispatchedSince), pageSize,
                            pageNumber, outBoxTimeout),
                    MapListFunction);

                span?.AddTag("db.response.returned_rows", result.Count());
                return result;
            }
            finally
            {
                Tracer?.EndSpan(span);
            }
        }

        /// <summary>
        /// Gets the specified message
        /// </summary>
        /// <param name="messageIds">The Ids of the messages</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>        
        /// <param name="outBoxTimeout">How long to wait for the message before timing out</param>
        /// <param name="args">For outboxes that require additional parameters such as topic, provide an optional arg</param>
        /// <returns>The message</returns>
        public IEnumerable<Message> Get(
            IEnumerable<Id> messageIds,
            RequestContext requestContext,
            int outBoxTimeout = -1,
            Dictionary<string, object>? args = null)
        {
            var dbAttributes = new Dictionary<string, string>()
            {
                { "db.operation.parameter.message.ids", string.Join(",", messageIds) },
                { "db.operation.name", ExtractSqlOperationName(queries.GetMessagesCommand) },
                { "db.query.text", queries.GetMessagesCommand }
            };
            var span = Tracer?.CreateDbSpan(
                new BoxSpanInfo(dbSystem, DatabaseConfiguration.DatabaseName, BoxDbOperation.Get, DatabaseConfiguration.OutBoxTableName,
                    dbAttributes: dbAttributes),
                requestContext?.Span,
                options: instrumentationOptions);

            try
            {
                var result = ReadFromStore(
                    connection => InitGetMessagesCommand(connection, messageIds.Select(m => m.ToString()).ToList(),
                        outBoxTimeout),
                    MapListFunction);

                span?.AddTag("db.response.returned_rows", result.Count());
                return result;
            }
            finally
            {
                Tracer?.EndSpan(span);
            }
        }

        /// <summary>
        /// Gets the specified message
        /// </summary>
        /// <param name="messageId">The id of the message to get</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>        
        /// <param name="outBoxTimeout">How long to wait for the message before timing out</param>
        /// <param name="args">For outboxes that require additional parameters such as topic, provide an optional arg</param>
        /// <returns>The message</returns>
        public Message Get(
            Id messageId,
            RequestContext requestContext,
            int outBoxTimeout = -1,
            Dictionary<string, object>? args = null
        )
        {
            var dbAttributes = new Dictionary<string, string>()
            {
                { "db.operation.parameter.message.id", messageId.Value },
                { "db.operation.name", ExtractSqlOperationName(queries.GetMessageCommand) },
                { "db.query.text", queries.GetMessageCommand }
            };
            var span = Tracer?.CreateDbSpan(
                new BoxSpanInfo(dbSystem, DatabaseConfiguration.DatabaseName, BoxDbOperation.Get, DatabaseConfiguration.OutBoxTableName,
                    dbAttributes: dbAttributes),
                requestContext?.Span,
                options: instrumentationOptions);

            try
            {
                var message = ReadFromStore(connection => InitGetMessageCommand(connection, messageId, outBoxTimeout),
                    MapFunction);

                return message;
            }
            finally
            {
                Tracer?.EndSpan(span);
            }
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
            Id messageId,
            RequestContext requestContext,
            int outBoxTimeout = -1,
            Dictionary<string, object>? args = null,
            CancellationToken cancellationToken = default)
        {
            var dbAttributes = new Dictionary<string, string>()
            {
                { "db.operation.parameter.message.id", messageId.Value },
                { "db.operation.name", ExtractSqlOperationName(queries.GetMessageCommand) },
                { "db.query.text", queries.GetMessageCommand }
            };
            var span = Tracer?.CreateDbSpan(
                new BoxSpanInfo(dbSystem, DatabaseConfiguration.DatabaseName, BoxDbOperation.Get, DatabaseConfiguration.OutBoxTableName,
                    dbAttributes: dbAttributes),
                requestContext?.Span,
                options: instrumentationOptions);

            try
            {
                var message = await ReadFromStoreAsync(
                    connection => InitGetMessageCommand(connection, messageId, outBoxTimeout),
                    dr => MapFunctionAsync(dr, cancellationToken), cancellationToken);

                return message;
            }
            finally
            {
                Tracer?.EndSpan(span);
            }
        }

        /// <summary>
        /// Returns messages specified by the Ids
        /// </summary>
        /// <param name="messageIds">The Ids of the messages</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>        
        /// <param name="outBoxTimeout">The Timeout of the outbox.</param>
        /// <param name="cancellationToken">Cancellation Token.</param>
        /// <returns></returns>
        public async Task<IEnumerable<Message>> GetAsync(
            IEnumerable<Id> messageIds,
            RequestContext requestContext,
            int outBoxTimeout = -1,
            CancellationToken cancellationToken = default
        )
        {
            var dbAttributes = new Dictionary<string, string>()
            {
                { "db.operation.parameter.message.ids", string.Join(",", messageIds.Select(id => id.Value)) },
                { "db.operation.name", ExtractSqlOperationName(queries.GetMessagesCommand) },
                { "db.query.text", queries.GetMessagesCommand }
            };
            var span = Tracer?.CreateDbSpan(
                new BoxSpanInfo(dbSystem, DatabaseConfiguration.DatabaseName, BoxDbOperation.Get, DatabaseConfiguration.OutBoxTableName,
                    dbAttributes: dbAttributes),
                requestContext?.Span,
                options: instrumentationOptions);

            try
            {
                var result = await ReadFromStoreAsync(
                    connection =>
                        InitGetMessagesCommand(connection, messageIds.Select(m => m.Value).ToList(), outBoxTimeout),
                    async (dr) => await MapListFunctionAsync(dr, cancellationToken), cancellationToken);

                span?.AddTag("db.response.returned_rows", result.Count());
                return result;
            }
            finally
            {
                Tracer?.EndSpan(span);
            }
        }

        /// <summary>
        /// Returns all messages in the store
        /// </summary>
        /// <param name="pageSize">Number of messages to return in search results (default = 100)</param>
        /// <param name="pageNumber">Page number of results to return (default = 1)</param>
        /// <param name="args">Additional parameters required for search, if any</param>
        /// <returns>A list of messages</returns>
        public IList<Message> Get(RequestContext? requestContext, int pageSize = 100, int pageNumber = 1,
            Dictionary<string, object>? args = null)
        {
            var dbAttributes = new Dictionary<string, string>()
            {
                { "db.operation.name", ExtractSqlOperationName(queries.PagedReadCommand) },
                { "db.query.text", queries.PagedReadCommand }
            };
            var span = Tracer?.CreateDbSpan(
                new BoxSpanInfo(dbSystem, DatabaseConfiguration.DatabaseName, BoxDbOperation.Get, DatabaseConfiguration.OutBoxTableName,
                    dbAttributes: dbAttributes),
                requestContext?.Span,
                options: instrumentationOptions);

            try
            {
                var result = ReadFromStore(connection => CreatePagedReadCommand(connection, pageSize, pageNumber),
                    MapListFunction).ToList();

                span?.AddTag("db.response.returned_rows", result.Count);
                return result;
            }
            finally
            {
                Tracer?.EndSpan(span);
            }
        }

        /// <summary>
        /// Returns all messages in the store
        /// </summary>
        /// <param name="requestContext">The context from the request pipeline</param>
        /// <param name="pageSize">Number of messages to return in search results (default = 100)</param>
        /// <param name="pageNumber">Page number of results to return (default = 1)</param>
        /// <param name="args">Additional parameters required for search, if any</param>
        /// <param name="cancellationToken">Cancellation Token</param>
        /// <returns></returns>
        public async Task<IList<Message>> GetAsync(
            RequestContext? requestContext,
            int pageSize = 100,
            int pageNumber = 1,
            Dictionary<string, object>? args = null,
            CancellationToken cancellationToken = default)
        {
            var dbAttributes = new Dictionary<string, string>()
            {
                { "db.operation.name", ExtractSqlOperationName(queries.PagedReadCommand) },
                { "db.query.text", queries.PagedReadCommand }
            };
            var span = Tracer?.CreateDbSpan(
                new BoxSpanInfo(dbSystem, DatabaseConfiguration.DatabaseName, BoxDbOperation.Get, DatabaseConfiguration.OutBoxTableName,
                    dbAttributes: dbAttributes),
                requestContext?.Span,
                options: instrumentationOptions);

            try
            {
                var result = (await ReadFromStoreAsync(
                    connection => CreatePagedReadCommand(connection, pageSize, pageNumber),
                    dr => MapListFunctionAsync(dr, cancellationToken), cancellationToken)).ToList();

                span?.AddTag("db.response.returned_rows", result.Count);
                return result;
            }
            finally
            {
                Tracer?.EndSpan(span);
            }
        }

        /// <summary>
        /// Get the number of messages in the Outbox that are not dispatched
        /// </summary>
        /// <param name="cancellationToken">Cancel the async operation</param>
        /// <returns></returns>
        public async Task<int> GetNumberOfOutstandingMessagesAsync(RequestContext? requestContext,
            CancellationToken cancellationToken = default)
        {
            var dbAttributes = new Dictionary<string, string>()
            {
                { "db.operation.name", ExtractSqlOperationName(queries.GetNumberOfOutstandingMessagesCommand) },
                { "db.query.text", queries.GetNumberOfOutstandingMessagesCommand }
            };
            var span = Tracer?.CreateDbSpan(
                new BoxSpanInfo(dbSystem, DatabaseConfiguration.DatabaseName, BoxDbOperation.Get, DatabaseConfiguration.OutBoxTableName,
                    dbAttributes: dbAttributes),
                requestContext?.Span,
                options: instrumentationOptions);

            try
            {
                return await ReadFromStoreAsync(CreateRemainingOutstandingCommand,
                    dr => MapOutstandingCountAsync(dr, cancellationToken), cancellationToken);
            }
            finally
            {
                Tracer?.EndSpan(span);
            }
        }

        /// <summary>
        /// Get the number of messages in the Outbox that are not dispatched
        /// </summary>
        /// <returns></returns>
        public int GetNumberOfOutstandingMessages(RequestContext? requestContext)
        {
            var dbAttributes = new Dictionary<string, string>()
            {
                { "db.operation.name", ExtractSqlOperationName(queries.GetNumberOfOutstandingMessagesCommand) },
                { "db.query.text", queries.GetNumberOfOutstandingMessagesCommand }
            };
            var span = Tracer?.CreateDbSpan(
                new BoxSpanInfo(dbSystem, DatabaseConfiguration.DatabaseName, BoxDbOperation.Get, DatabaseConfiguration.OutBoxTableName,
                    dbAttributes: dbAttributes),
                requestContext?.Span,
                options: instrumentationOptions);

            try
            {
                return ReadFromStore(CreateRemainingOutstandingCommand, MapOutstandingCount);
            }
            finally
            {
                Tracer?.EndSpan(span);
            }
        }

        /// <summary>
        /// Update a message to show it is dispatched
        /// </summary>
        /// <param name="id">The id of the message to update</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>
        /// <param name="dispatchedAt">When was the message dispatched, defaults to UTC now</param>
        /// <param name="args">Allows additional arguments for specific Outbox Db providers</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        public async Task MarkDispatchedAsync(
            Id id,
            RequestContext? requestContext,
            DateTimeOffset? dispatchedAt = null,
            Dictionary<string, object>? args = null,
            CancellationToken cancellationToken = default)
        {
            var dbAttributes = new Dictionary<string, string>()
            {
                { "db.operation.parameter.message.id", id.Value },
                { "db.operation.name", ExtractSqlOperationName(queries.MarkDispatchedCommand) },
                { "db.query.text", queries.MarkDispatchedCommand }
            };
            var span = Tracer?.CreateDbSpan(
                new BoxSpanInfo(dbSystem, DatabaseConfiguration.DatabaseName, BoxDbOperation.MarkDispatched, DatabaseConfiguration.OutBoxTableName,
                    dbAttributes: dbAttributes),
                requestContext?.Span,
                options: instrumentationOptions);

            try
            {
                await WriteToStoreAsync(null,
                    connection => InitMarkDispatchedCommand(connection, id, dispatchedAt ?? DateTimeOffset.UtcNow),
                    null,
                    cancellationToken);
            }
            finally
            {
                Tracer?.EndSpan(span);
            }
        }

        /// <summary>
        /// Update messages to show it is dispatched
        /// </summary>
        /// <param name="ids">The ids of the messages to update</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>
        /// <param name="dispatchedAt">When was the message dispatched, defaults to UTC now</param>
        /// <param name="args">Allows additional arguments to be passed for specific Db providers</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        public async Task MarkDispatchedAsync(
            IEnumerable<Id> ids,
            RequestContext? requestContext,
            DateTimeOffset? dispatchedAt = null,
            Dictionary<string, object>? args = null,
            CancellationToken cancellationToken = default)
        {
            var dbAttributes = new Dictionary<string, string>()
            {
                { "db.operation.parameter.message.ids", string.Join(",", ids.Select(m => m.Value)) },
                { "db.operation.name", ExtractSqlOperationName(queries.MarkMultipleDispatchedCommand) },
                { "db.query.text", queries.MarkMultipleDispatchedCommand }
            };
            var span = Tracer?.CreateDbSpan(
                new BoxSpanInfo(dbSystem, DatabaseConfiguration.DatabaseName, BoxDbOperation.MarkDispatched, DatabaseConfiguration.OutBoxTableName,
                    dbAttributes: dbAttributes),
                requestContext?.Span,
                options: instrumentationOptions);

            try
            {
                await WriteToStoreAsync(null,
                    connection => InitMarkDispatchedCommand(connection, ids, dispatchedAt ?? DateTimeOffset.UtcNow),
                    null,
                    cancellationToken);
            }
            finally
            {
                Tracer?.EndSpan(span);
            }
        }

        /// <summary>
        /// Update a message to show it is dispatched
        /// </summary>
        /// <param name="id">The id of the message to update</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>        
        /// <param name="dispatchedAt">When was the message dispatched, defaults to UTC now</param>
        /// <param name="args">Allows additional arguments to be provided for specific Outbox Db providers</param>
        public void MarkDispatched(
            Id id,
            RequestContext requestContext,
            DateTimeOffset? dispatchedAt = null,
            Dictionary<string, object>? args = null)
        {
            var dbAttributes = new Dictionary<string, string>()
            {
                { "db.operation.parameter.message.id", id.Value },
                { "db.operation.name", ExtractSqlOperationName(queries.MarkDispatchedCommand) },
                { "db.query.text", queries.MarkDispatchedCommand }
            };
            var span = Tracer?.CreateDbSpan(
                new BoxSpanInfo(dbSystem, DatabaseConfiguration.DatabaseName, BoxDbOperation.MarkDispatched, DatabaseConfiguration.OutBoxTableName,
                    dbAttributes: dbAttributes),
                requestContext?.Span,
                options: instrumentationOptions);

            try
            {
                WriteToStore(null,
                    connection => InitMarkDispatchedCommand(connection, id, dispatchedAt ?? DateTime.UtcNow),
                    null);
            }
            finally
            {
                Tracer?.EndSpan(span);
            }
        }

        /// <summary>
        /// Messages still outstanding in the Outbox because their timestamp
        /// </summary>
        /// <param name="dispatchedSince">How many seconds since the message was sent do we wait to declare it outstanding</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>        
        /// <param name="pageSize">The number of entries on a page</param>
        /// <param name="pageNumber">The page to return</param>
        /// <param name="trippedTopics">Collection of tripped topics</param>
        /// <param name="args">Additional parameters required for search, if any</param>
        /// <returns>Outstanding Messages</returns>
        public IEnumerable<Message> OutstandingMessages(
            TimeSpan dispatchedSince,
            RequestContext? requestContext,
            int pageSize = 100,
            int pageNumber = 1,
            IEnumerable<RoutingKey>? trippedTopics = null,
            Dictionary<string, object>? args = null)
        {
            var dbAttributes = new Dictionary<string, string>()
            {
                { "db.operation.name", ExtractSqlOperationName(queries.PagedOutstandingCommand) },
                { "db.query.text", queries.PagedOutstandingCommand }
            };
            var span = Tracer?.CreateDbSpan(
                new BoxSpanInfo(dbSystem, DatabaseConfiguration.DatabaseName, BoxDbOperation.OutStandingMessages, DatabaseConfiguration.OutBoxTableName,
                    dbAttributes: dbAttributes),
                requestContext?.Span,
                options: instrumentationOptions);

            try
            {
                var result = ReadFromStore(
                    connection => CreatePagedOutstandingCommand(connection, dispatchedSince, pageSize, pageNumber, trippedTopics ?? [], - 1),
                    MapListFunction);

                span?.AddTag("db.response.returned_rows", result.Count());
                return result;
            }
            finally
            {
                Tracer?.EndSpan(span);
            }
        }

        /// <summary>
        /// Messages still outstanding in the Outbox because their timestamp
        /// </summary>
        /// <param name="dispatchedSince">How many seconds since the message was sent do we wait to declare it outstanding</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>
        /// <param name="pageSize">The number of entries to return in a page</param>
        /// <param name="pageNumber">The page number to return</param>
        /// <param name="trippedTopics">Collection of tripped topics</param>
        /// <param name="args">Additional parameters required for search, if any</param>
        /// <param name="cancellationToken">Async Cancellation Token</param>
        /// <returns>Outstanding Messages</returns>
        public async Task<IEnumerable<Message>> OutstandingMessagesAsync(
            TimeSpan dispatchedSince,
            RequestContext requestContext,
            int pageSize = 100,
            int pageNumber = 1,
            IEnumerable<RoutingKey>? trippedTopics = null,
            Dictionary<string, object>? args = null,
            CancellationToken cancellationToken = default)
        {
            var dbAttributes = new Dictionary<string, string>()
            {
                { "db.operation.name", ExtractSqlOperationName(queries.PagedOutstandingCommand) },
                { "db.query.text", queries.PagedOutstandingCommand }
            };
            var span = Tracer?.CreateDbSpan(
                new BoxSpanInfo(dbSystem, DatabaseConfiguration.DatabaseName, BoxDbOperation.OutStandingMessages, DatabaseConfiguration.OutBoxTableName,
                    dbAttributes: dbAttributes),
                requestContext?.Span,
                options: instrumentationOptions);

            try
            {
                var result = await ReadFromStoreAsync(
                    connection => CreatePagedOutstandingCommand(connection, dispatchedSince, pageSize, pageNumber, trippedTopics ?? [],  -1),
                    dr => MapListFunctionAsync(dr, cancellationToken), cancellationToken);

                span?.AddTag("db.response.returned_rows", result.Count());
                return result;
            }
            finally
            {
                Tracer?.EndSpan(span);
            }
        }

        protected virtual void WriteToStore(
            IAmABoxTransactionProvider<DbTransaction>? transactionProvider,
            Func<DbConnection, DbCommand> commandFunc,
            Action? loggingAction
        )
        {
            var connection = GetOpenConnection(ConnectionProvider, transactionProvider);

            using var command = commandFunc.Invoke(connection);
            try
            {
                if (transactionProvider is { HasOpenTransaction: true })
                {
                    command.Transaction = transactionProvider.GetTransaction();
                }

                command.ExecuteNonQuery();
            }
            catch (DbException exception)
            {
                if (!IsExceptionUniqueOrDuplicateIssue(exception))
                {
                    throw;
                }

                loggingAction?.Invoke();
                Log.DuplicateDetectedInBatch(logger);
            }
            finally
            {
                FinishWrite(connection, transactionProvider);
            }
        }

        protected virtual async Task WriteToStoreAsync(
            IAmABoxTransactionProvider<DbTransaction>? transactionProvider,
            Func<DbConnection, DbCommand> commandFunc,
            Action? loggingAction,
            CancellationToken cancellationToken
        )
        {
            var connection = await GetOpenConnectionAsync(ConnectionProvider, transactionProvider, cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);

#if NETSTANDARD
            using var command = commandFunc.Invoke(connection);
#else
            await using var command = commandFunc.Invoke(connection);
#endif

            try
            {
                if (transactionProvider is { HasOpenTransaction: true })
                {
                    command.Transaction = await transactionProvider.GetTransactionAsync(cancellationToken)
                        .ConfigureAwait(ContinueOnCapturedContext);
                }

                await command
                    .ExecuteNonQueryAsync(cancellationToken)
                    .ConfigureAwait(ContinueOnCapturedContext);
            }
            catch (DbException exception)
            {
                if (!IsExceptionUniqueOrDuplicateIssue(exception))
                {
                    throw;
                }
                
                loggingAction?.Invoke();
            }
            finally
            {
                FinishWrite(connection, transactionProvider);
            }
        }

        protected abstract bool IsExceptionUniqueOrDuplicateIssue(Exception ex);

        protected virtual T ReadFromStore<T>(
            Func<DbConnection, DbCommand> commandFunc,
            Func<DbDataReader, T> resultFunc
        )
        {
            var connection = GetOpenConnection(ConnectionProvider, null);
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

        protected virtual async Task<T> ReadFromStoreAsync<T>(
            Func<DbConnection, DbCommand> commandFunc,
            Func<DbDataReader, Task<T>> resultFunc,
            CancellationToken cancellationToken
        )
        {
            var connection = await GetOpenConnectionAsync(ConnectionProvider, null, cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);
            
#if NETSTANDARD
            using var command = commandFunc.Invoke(connection);
#else
            await using var command = commandFunc.Invoke(connection);
#endif
            try
            {
                var dr = await command.ExecuteReaderAsync(cancellationToken)
                    .ConfigureAwait(ContinueOnCapturedContext);
                
                return await resultFunc.Invoke(dr)
                    .ConfigureAwait(ContinueOnCapturedContext);
            }
            finally
            {
#if NETSTANDARD
                connection.Close();
#else
                await connection
                    .CloseAsync()
                    .ConfigureAwait(ContinueOnCapturedContext);
#endif
            }
        }

        protected virtual DbConnection GetOpenConnection(IAmARelationalDbConnectionProvider defaultConnectionProvider,
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

        protected virtual void FinishWrite(DbConnection connection,
            IAmABoxTransactionProvider<DbTransaction>? transactionProvider)
        {
            if (transactionProvider != null)
                transactionProvider.Close();
            else
                connection.Close();
        }

        protected virtual async Task<DbConnection> GetOpenConnectionAsync(
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
            IEnumerable<RoutingKey> trippedTopics,
            int outboxTimeout)
        {
            var inClause = GeneratePagedOutstandingCommandInStatementAndAddParameters(trippedTopics.ToList());

            return CreateCommand(connection, GenerateSqlText(queries.PagedOutstandingCommand, inClause.inClause), outboxTimeout,
                CreatePagedOutstandingParameters(timeSinceAdded, pageSize, pageNumber, inClause.parameters));
        }

        private (string inClause, IDbDataParameter[] parameters) GeneratePagedOutstandingCommandInStatementAndAddParameters(
            IEnumerable<RoutingKey> topics)
        {
            var topicsList = topics.Select(x => x.Value).ToList();
            var inClause = GenerateInClauseAndAddParameters(topicsList);
            
            var inClauseSql = topicsList.Count > 0
                ? string.Format(queries.PagedOutstandingCommandInStatement, inClause.inClause)
                : string.Empty;

            return (inClauseSql, inClause.parameters);
        }

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

        private DbCommand InitMarkDispatchedCommand(DbConnection connection, Id messageId, DateTimeOffset? dispatchedAt)
            => CreateCommand(connection, GenerateSqlText(queries.MarkDispatchedCommand), 0,
                CreateSqlParameter("MessageId", messageId.Value),
                CreateSqlParameter("DispatchedAt", dispatchedAt?.ToUniversalTime()));

        private DbCommand InitMarkDispatchedCommand(DbConnection connection, IEnumerable<Id> messageIds,
            DateTimeOffset? dispatchedAt)
        {
            var inClause = GenerateInClauseAndAddParameters(messageIds.Select(m => m.ToString()).ToList());
            return CreateCommand(connection, GenerateSqlText(queries.MarkMultipleDispatchedCommand, inClause.inClause),
                0,
                inClause.parameters.Append(CreateSqlParameter("DispatchedAt", dispatchedAt?.ToUniversalTime()))
                    .ToArray());
        }

        private DbCommand InitGetMessageCommand(DbConnection connection, Id messageId, int outBoxTimeout = -1)
            => CreateCommand(connection, GenerateSqlText(queries.GetMessageCommand), outBoxTimeout,
                CreateSqlParameter("MessageId", messageId.Value));

        private DbCommand InitGetMessagesCommand(DbConnection connection, List<string> messageIds,
            int outBoxTimeout = -1)
        {
            var inClause = GenerateInClauseAndAddParameters(messageIds);
            return CreateCommand(connection, GenerateSqlText(queries.GetMessagesCommand, inClause.inClause),
                outBoxTimeout,
                inClause.parameters);
        }

        private string GenerateSqlText(string sqlFormat, params string[] orderedParams)
            => string.Format(sqlFormat, orderedParams.Prepend(DatabaseConfiguration.OutBoxTableName).ToArray());

        private DbCommand InitDeleteDispatchedCommand(DbConnection connection, IEnumerable<string> messageIds)
        {
            var inClause = GenerateInClauseAndAddParameters(messageIds.ToList());
            return CreateCommand(connection, GenerateSqlText(queries.DeleteMessagesCommand, inClause.inClause), 0,
                inClause.parameters);
        }

        protected virtual DbCommand CreateCommand(DbConnection connection, string sqlText, int outBoxTimeout,
            params IDbDataParameter[] parameters)
        {
            var command = connection.CreateCommand();

            command.CommandTimeout = outBoxTimeout < 0 ? 0 : outBoxTimeout;
            command.CommandText = sqlText;
            command.Parameters.AddRange(parameters);

            return command;
        }


        protected virtual IDbDataParameter[] CreatePagedOutstandingParameters(TimeSpan since, int pageSize,
            int pageNumber, IDbDataParameter[] inParams)
        {
            var parameters = new IDbDataParameter[3];
            parameters[0] = CreateSqlParameter("@Skip", Math.Max(pageNumber - 1, 0) * pageSize);
            parameters[1] = CreateSqlParameter("@Take", pageSize);
            parameters[2] = CreateSqlParameter("@TimestampSince", DateTimeOffset.UtcNow.Subtract(since));

            return parameters.Concat(inParams).ToArray();
        }

        protected virtual IDbDataParameter[] CreatePagedDispatchedParameters(TimeSpan dispatchedSince, int pageSize, int pageNumber)
        {
            
            var parameters = new IDbDataParameter[3];
            parameters[0] = CreateSqlParameter("@Skip", Math.Max(pageNumber - 1, 0) * pageSize);
            parameters[1] = CreateSqlParameter("@Take", pageSize);
            parameters[2] = CreateSqlParameter("@DispatchedSince", DateTimeOffset.UtcNow.Subtract(dispatchedSince));

            return parameters;
        }

        protected virtual IDbDataParameter[] CreatePagedReadParameters(int pageSize, int pageNumber)
        {
            var parameters = new IDbDataParameter[2];
            parameters[0] = CreateSqlParameter("@Skip", Math.Max(pageNumber - 1, 0) * pageSize);
            parameters[1] = CreateSqlParameter("@Take", pageSize);

            return parameters;
        }

        protected abstract IDbDataParameter CreateSqlParameter(string parameterName, object? value);
        protected abstract IDbDataParameter CreateSqlParameter(string parameterName, DbType dbType, object? value);

        protected virtual IDbDataParameter[] InitAddDbParameters(Message message, int? position = null)
        {
            var prefix = position.HasValue ? $"p{position}_" : "";
            var bagJson = JsonSerializer.Serialize(message.Header.Bag, JsonSerialisationOptions.Options);
            
            var body = DatabaseConfiguration.BinaryMessagePayload ? 
                CreateSqlParameter($"@{prefix}Body", DbType.Binary, message.Body.Bytes)
                : CreateSqlParameter($"@{prefix}Body", DbType.String, message.Body.Value);

            return
            [
                body,
                CreateSqlParameter($"@{prefix}MessageId", DbType.String, message.Id.Value),
                CreateSqlParameter($"@{prefix}MessageType", DbType.String, message.Header.MessageType.ToString()),
                CreateSqlParameter($"@{prefix}Topic", DbType.String, message.Header.Topic.Value),
                CreateSqlParameter($"@{prefix}Timestamp", DbType.DateTimeOffset, message.Header.TimeStamp.ToUniversalTime()),
                CreateSqlParameter($"@{prefix}CorrelationId", DbType.String, message.Header.CorrelationId.Value),
                CreateSqlParameter($"@{prefix}ReplyTo", DbType.String, message.Header.ReplyTo?.Value),
                CreateSqlParameter($"@{prefix}ContentType", DbType.String, message.Header.ContentType.ToString()),
                CreateSqlParameter($"@{prefix}PartitionKey", DbType.String, message.Header.PartitionKey.Value),
                CreateSqlParameter($"@{prefix}HeaderBag", DbType.String, bagJson),
                CreateSqlParameter($"@{prefix}Source", DbType.String, message.Header.Source.ToString()),
                CreateSqlParameter($"@{prefix}Type", DbType.String, message.Header.Type.Value),
                CreateSqlParameter($"@{prefix}DataSchema", DbType.String, message.Header.DataSchema?.ToString()),
                CreateSqlParameter($"@{prefix}Subject", DbType.String, message.Header.Subject),
                CreateSqlParameter($"@{prefix}TraceParent", DbType.String, message.Header.TraceParent?.Value),
                CreateSqlParameter($"@{prefix}TraceState", DbType.String, message.Header.TraceState?.Value),
                CreateSqlParameter($"@{prefix}Baggage", DbType.String, message.Header.Baggage.ToString()),
                CreateSqlParameter($"@{prefix}WorkflowId", DbType.String, message.Header.WorkflowId?.Value),
                CreateSqlParameter($"@{prefix}JobId", DbType.String, message.Header.JobId?.Value),
            ];
        }

        protected virtual Message MapFunction(DbDataReader dr)
        {
            return dr.Read() ? MapAMessage(dr) : new Message();
        }

        protected virtual async Task<Message> MapFunctionAsync(DbDataReader dr, CancellationToken cancellationToken)
        {
            if (await dr.ReadAsync(cancellationToken)
                    .ConfigureAwait(ContinueOnCapturedContext))
            {
                return MapAMessage(dr);
            }

            return new Message();}

        protected virtual IEnumerable<Message> MapListFunction(DbDataReader dr)
        {
            var messages = new List<Message>();
            while (dr.Read())
            {
                messages.Add(MapAMessage(dr));
            }

            dr.Close();

            return messages;
        }

        protected virtual async Task<IEnumerable<Message>> MapListFunctionAsync(DbDataReader dr,
            CancellationToken cancellationToken)
        {
            
            var messages = new List<Message>();
            while (await dr.ReadAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext))
            {
                messages.Add(MapAMessage(dr));
            }
            
#if NETSTANDARD2_0
            dr.Close();
#else
            await dr.CloseAsync().ConfigureAwait(ContinueOnCapturedContext);
#endif

            return messages;
        }

        protected virtual int MapOutstandingCount(DbDataReader dr)
        {
            int outstandingMessages = -1;
            if (dr.Read())
            {
                outstandingMessages = dr.GetInt32(0);
            }

            dr.Close();
            return outstandingMessages;
        }
        
        protected virtual async Task<int> MapOutstandingCountAsync(DbDataReader dr, CancellationToken cancellationToken)
        {
            int outstandingMessages = -1;
            if (await dr.ReadAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext))
            {
                outstandingMessages = dr.GetInt32(0);
            }

#if NETSTANDARD2_0
            dr.Close();
#else
            await dr.CloseAsync().ConfigureAwait(ContinueOnCapturedContext);
#endif

            return outstandingMessages;
        }

        private (string inClause, IDbDataParameter[] parameters) GenerateInClauseAndAddParameters(
            List<string> messageIds)
        {
            var paramNames = messageIds.Select((s, i) => "@p" + i).ToArray();

            var parameters = new IDbDataParameter[messageIds.Count];
            for (int i = 0; i < paramNames.Length; i++)
            {
                parameters[i] = CreateSqlParameter(paramNames[i], messageIds[i]);
            }

            return (string.Join(",", paramNames), parameters);
        }

        private (string insertClause, IDbDataParameter[] parameters) GenerateBulkInsert(List<Message> messages)
        {
            var messageParams = new List<string>();
            var parameters = new List<IDbDataParameter>();

            for (int i = 0; i < messages.Count; i++)
            {
                // include all columns in the same order as the CREATE TABLE DDL:
                messageParams.Add(
                    $"(@p{i}_MessageId, @p{i}_MessageType, @p{i}_Topic, @p{i}_Timestamp, @p{i}_CorrelationId, " +
                    $"@p{i}_ReplyTo, @p{i}_ContentType, @p{i}_PartitionKey, @p{i}_HeaderBag, @p{i}_Body, " +
                    $"@p{i}_Source, @p{i}_Type, @p{i}_DataSchema, @p{i}_Subject, @p{i}_TraceParent, @p{i}_TraceState, " +  
                    $"@p{i}_Baggage, @p{i}_WorkflowId, @p{i}_JobId)");

                parameters.AddRange(InitAddDbParameters(messages[i], i));
            }

            return (string.Join(",", messageParams), parameters.ToArray());
        }

        private static string ExtractSqlOperationName(string queryText)
        {
            return queryText.Split(' ')[0];
        }
        
        protected virtual Message MapAMessage(DbDataReader dr)
        {
            var header = new MessageHeader(
                messageId:  GetMessageId(dr),
                topic: GetTopic(dr),
                messageType: GetMessageType(dr),
                source: GetSource(dr),
                type: GetEventType(dr),
                timeStamp: GetTimeStamp(dr),
                correlationId: GetCorrelationId(dr),
                replyTo:  GetReplyTo(dr),
                contentType: GetContentType(dr),
                partitionKey: GetPartitionKey(dr),
                dataSchema: GetDataSchema(dr),
                subject: GetSubject(dr),
                handledCount: 0, // HandledCount is zero when restored from the Outbox
                workflowId: GetWorkflowId(dr),
                jobId: GetJobId(dr),
                delayed: TimeSpan.Zero, // Delayed is zero when restored from the Outbox
                traceParent: GetTraceParent(dr),
                traceState:  GetTraceState(dr),
                baggage:  GetBaggage(dr)
            )
            {
                SpecVersion = GetSpecVersion(dr), 
                DataRef = GetDataRef(dr)
            };
            
            Dictionary<string, object>? dictionaryBag = GetContextBag(dr);
            if (dictionaryBag != null)
            {
                foreach (var keyValue in dictionaryBag)
                {
                    header.Bag.Add(keyValue.Key, keyValue.Value);
                }
            }

            var body = DatabaseConfiguration.BinaryMessagePayload
                ? new MessageBody(GetBodyAsByteArray(dr))
                : new MessageBody(GetBodyAsString(dr));

            return new Message(header, body);
        }
        
        protected virtual bool TryGetOrdinal(DbDataReader dr, string columnName, out int ordinal)
        {
            try
            {
                ordinal = dr.GetOrdinal(columnName);
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                ordinal = -1;
                return false;
            }
            catch (IndexOutOfRangeException)
            {
                // SpecVersion column does not exist, return -1 and true to indicate error
                ordinal = -1;
                return false;
            }
        }

        protected virtual string BodyColumnName => "Body";
        protected virtual byte[] GetBodyAsByteArray(DbDataReader dr)
        {
            var body = dr.GetStream(dr.GetOrdinal(BodyColumnName));
            if (body is MemoryStream memoryStream) // No need to dispose a MemoryStream, I do not think they dare to ever change that
            {
                return memoryStream.ToArray(); // Then we can just return its value, instead of copying manually
            }

            var ms = new MemoryStream();
            body.CopyTo(ms);
            body.Dispose();
            return ms.ToArray();
        }

        protected virtual string GetBodyAsString(DbDataReader dr) => dr.GetString(dr.GetOrdinal(BodyColumnName));
        

        protected virtual string BaggageColumnName => "Baggage";
        protected virtual Baggage GetBaggage(DbDataReader dr)
        {
            var baggage = new Baggage();
            if (!TryGetOrdinal(dr, BaggageColumnName, out var ordinal) || dr.IsDBNull(ordinal))
            {
                return baggage;
            }

            var baggageString = dr.GetString(ordinal);
            if (string.IsNullOrEmpty(baggageString))
            {
                return baggage;
            }
           
            baggage.LoadBaggage(baggageString);
            return baggage;
        }

        protected virtual string ContentTypeColumnName => "ContentType";
        protected virtual ContentType GetContentType(DbDataReader dr)
        {
            if (!TryGetOrdinal(dr, ContentTypeColumnName, out var ordinal) || dr.IsDBNull(ordinal))
            {
                return new ContentType(MediaTypeNames.Text.Plain);
            }
            
            var contentType = dr.GetString(ordinal);
            return string.IsNullOrEmpty(contentType) ? new ContentType(MediaTypeNames.Text.Plain) : new ContentType(contentType);
        }

        protected virtual string HeaderBagColumnName => "HeaderBag";
        protected virtual Dictionary<string, object>? GetContextBag(DbDataReader dr)
        {
            if (!TryGetOrdinal(dr, HeaderBagColumnName, out var ordinal) || dr.IsDBNull(ordinal))
            {
                return new Dictionary<string, object>();
            }

            var headerBag = dr.GetString(ordinal);
            var dictionaryBag = JsonSerializer.Deserialize<Dictionary<string, object>>(headerBag, JsonSerialisationOptions.Options);
            return dictionaryBag;
        }

        protected virtual string CorrelationIdColumnName => "CorrelationId";
        protected virtual Id GetCorrelationId(DbDataReader dr)
        {
            if (!TryGetOrdinal(dr, CorrelationIdColumnName, out var ordinal) || dr.IsDBNull(ordinal))
            {
                return Id.Empty;
            }
            
            var correlationId = dr.GetString(ordinal);
            if (string.IsNullOrEmpty(correlationId))
            {
                return Id.Empty;
            }
            
            return new Id(correlationId);
        }
        
        protected virtual string DataRefColumnName => "DataRef";
        protected virtual string? GetDataRef(DbDataReader dr)
        {
            if (!TryGetOrdinal(dr, DataRefColumnName, out var ordinal) || dr.IsDBNull(ordinal))
            {
                return null;
            }
            
            return dr.GetString(ordinal);
        }
        
        protected virtual string PartitionKeyColumnName => "PartitionKey";
        protected virtual PartitionKey GetPartitionKey(DbDataReader dr)
        {
            if (!TryGetOrdinal(dr, PartitionKeyColumnName, out var ordinal) || dr.IsDBNull(ordinal))
            {
                return string.Empty;
            }
            
            var partitionKey = dr.GetString(ordinal);
            return string.IsNullOrEmpty(partitionKey) ? PartitionKey.Empty : new PartitionKey(partitionKey);
        }
        
        protected virtual string DataSchemaColumnName => "DataSchema";
        protected virtual Uri? GetDataSchema(DbDataReader dr)
        {
            if (!TryGetOrdinal(dr, DataSchemaColumnName, out var ordinal) || dr.IsDBNull(ordinal))
            {
                return null;
            }

            return Uri.TryCreate(dr.GetString(ordinal), UriKind.Absolute, out var uri) ? uri : null;
        }
        
        protected virtual string TypeColumnName => "Type";
        protected virtual CloudEventsType GetEventType(DbDataReader dr)
        {
            if (!TryGetOrdinal(dr, TypeColumnName, out var ordinal) || dr.IsDBNull(ordinal))
                return CloudEventsType.Empty;
            
            var type = dr.GetString(ordinal);
            if (string.IsNullOrEmpty(type))
                return CloudEventsType.Empty;

            return new CloudEventsType(type);
        }
        
        protected virtual string JobIdColumnName => "JobId";

        protected virtual Id? GetJobId(DbDataReader dr)
        {
            if (!TryGetOrdinal(dr, JobIdColumnName, out var ordinal) || dr.IsDBNull(ordinal))
            {
                return null;
            }
            var jobId = dr.GetString(ordinal);
            return string.IsNullOrEmpty(jobId) ? null : new Id(jobId);
        }

        protected virtual string TopicColumnName => "Topic";
        protected virtual RoutingKey GetTopic(DbDataReader dr)
        {
            if (!TryGetOrdinal(dr, TopicColumnName, out var ordinal) || dr.IsDBNull(ordinal))
            {
                return RoutingKey.Empty;
            }
            
            var topic = dr.GetString(ordinal);
            if (string.IsNullOrEmpty(topic))
            {
                return RoutingKey.Empty;
            }
            
            return new RoutingKey(topic);
        }
        
        protected virtual string ReplyToColumnName => "ReplyTo";
        protected virtual RoutingKey? GetReplyTo(DbDataReader dr)
        {
            if (!TryGetOrdinal(dr, ReplyToColumnName, out var ordinal) || dr.IsDBNull(ordinal))
            {
                return null;
            }
            
            var topic = dr.GetString(ordinal);
            if (string.IsNullOrEmpty(topic))
            {
                return null;
            }
            
            return new RoutingKey(topic);
        }
        
        protected virtual string MessageTypeColumnName => "MessageType";
        protected virtual MessageType GetMessageType(DbDataReader dr)
        {
            if (!TryGetOrdinal(dr, MessageTypeColumnName, out var ordinal) || dr.IsDBNull(ordinal))
            {
                return MessageType.MT_NONE;
            }

            var value = dr.GetString(ordinal);
            if (string.IsNullOrEmpty(value))
            {
                return MessageType.MT_NONE;
            }
            
#if NETSTANDARD
            return (MessageType)Enum.Parse(typeof(MessageType), value);
#else
            return Enum.Parse<MessageType>(value);
#endif
        }
        
        protected virtual string MessageIdColumnName => "MessageId";
        protected virtual Id GetMessageId(DbDataReader dr)
        {
            if (!TryGetOrdinal(dr, MessageIdColumnName, out var ordinal) || dr.IsDBNull(ordinal))
            {
                return Id.Random();
            }
 
            var id = dr.GetString(ordinal);
            return new Id(id);
        }
        
        protected virtual string SourceColumnName => "Source";
        protected virtual Uri GetSource(DbDataReader dr)
        {
            if (!TryGetOrdinal(dr, SourceColumnName, out var ordinal) || dr.IsDBNull(ordinal))
            {
                return new Uri(MessageHeader.DefaultSource);
            }

            return Uri.TryCreate(dr.GetString(ordinal), UriKind.RelativeOrAbsolute, out var source) ? source : new Uri(MessageHeader.DefaultSource);
        }
        
        protected virtual string SpecVersionColumnName => "SpecVersion";
        protected virtual string GetSpecVersion(DbDataReader dr)
        {
            if (!TryGetOrdinal(dr, SpecVersionColumnName, out var ordinal) || dr.IsDBNull(ordinal))
            {
                return MessageHeader.DefaultSpecVersion;
            }
            
            var specVersion = dr.GetString(ordinal);
            return string.IsNullOrEmpty(specVersion) ? MessageHeader.DefaultSpecVersion : specVersion;
        }
        
        protected virtual string SubjectColumnName => "Subject";
        protected virtual string? GetSubject(DbDataReader dr)
        {
            if (!TryGetOrdinal(dr, SubjectColumnName, out var ordinal) || dr.IsDBNull(ordinal))
            {
                return null;
            }
            
            var subject = dr.GetString(ordinal);
            return string.IsNullOrEmpty(subject) ? null : subject;
        }
        
        protected virtual string TimestampColumnName => "Timestamp";
        protected virtual DateTimeOffset GetTimeStamp(DbDataReader dr)
        {
            if (!TryGetOrdinal(dr, TimestampColumnName, out var ordinal) || dr.IsDBNull(ordinal))
            {
                return DateTimeOffset.UtcNow;
            }

            var dataTime = dr.GetDateTime(ordinal);
            return dataTime;
        }
        
        protected virtual string TraceParentColumnName => "TraceParent";
        protected virtual TraceParent? GetTraceParent(DbDataReader dr)
        {
            if (!TryGetOrdinal(dr, TraceParentColumnName, out var ordinal) || dr.IsDBNull(ordinal))
            {
                return null;
            }
            
            var traceParent = dr.GetString(ordinal);
            return string.IsNullOrEmpty(traceParent) ? null : new TraceParent(traceParent);
        }

        protected virtual string TraceStateColumnName => "TraceState";
        protected virtual TraceState? GetTraceState(DbDataReader dr)
        {
            if (!TryGetOrdinal(dr, TraceStateColumnName, out var ordinal) || dr.IsDBNull(ordinal))
            {
                return null;
            }

            var traceState = dr.GetString(ordinal);
            return string.IsNullOrEmpty(traceState) ? null :  new TraceState(traceState);
        }
        
        protected virtual string WorkflowIdColumnName => "WorkflowId";

        protected virtual Id? GetWorkflowId(DbDataReader dr)
        {
            if (!TryGetOrdinal(dr, WorkflowIdColumnName, out var ordinal) || dr.IsDBNull(ordinal))
            {
                return null;
            }
            
            var workflowId = dr.GetString(ordinal);
            return string.IsNullOrEmpty(workflowId) ? null : new Id(workflowId);
        }

        private static partial class Log
        {
            [LoggerMessage(LogLevel.Warning, "A duplicate was detected in the batch")]
            public static partial void DuplicateDetectedInBatch(ILogger logger);
        }
    }
}
