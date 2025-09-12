#region Licence

/* The MIT License (MIT)
Copyright © 2015 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using Paramore.Brighter.DynamoDb;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Outbox.DynamoDB
{
    public class DynamoDbOutbox :
        IAmAnOutboxSync<Message, TransactWriteItemsRequest>,
        IAmAnOutboxAsync<Message, TransactWriteItemsRequest>
    {
        private readonly DynamoDbConfiguration _configuration;
        private readonly DynamoDBContext _context;
        private readonly DynamoDBOperationConfig _dynamoOverwriteTableConfig;
        private readonly Random _random = new Random();
        private readonly TimeProvider _timeProvider;

        private readonly ConcurrentDictionary<string, TopicQueryContext> _outstandingTopicQueryContexts;
        private readonly ConcurrentDictionary<string, TopicQueryContext> _dispatchedTopicQueryContexts;

        private AllTopicsScanContext _outstandingAllTopicsScanContext;
        private AllTopicsScanContext _dispatchedAllTopicsScanContext;

        private readonly InstrumentationOptions _instrumentationOptions;
        private const string DYNAMO_DB_NAME = "outbox";

        public bool ContinueOnCapturedContext { get; set; }
        
        /// <summary>
        /// The Tracer that we want to use to capture telemetry
        /// We inject this so that we can use the same tracer as the calling application
        /// You do not need to set this property as we will set it when setting up the External Service Bus
        /// </summary>
        public IAmABrighterTracer? Tracer { private get; set; }

        /// <summary>
        ///  Initialises a new instance of the <see cref="DynamoDbOutbox"/> class.
        /// </summary>
        /// <param name="client">The DynamoDBContext</param>
        /// <param name="configuration">The DynamoDB Operation Configuration</param>
        /// <param name="timeProvider">Provides a timer that can be overwritten in teests; on null uses system timer</param>
        /// <param name="instrumentationOptions"></param>
        public DynamoDbOutbox(
            IAmazonDynamoDB client,
            DynamoDbConfiguration configuration,
            TimeProvider? timeProvider = null,
            InstrumentationOptions instrumentationOptions = InstrumentationOptions.All)
        {
            _configuration = configuration;
            _context = new DynamoDBContext(client);
            _timeProvider = timeProvider ?? TimeProvider.System;
            _dynamoOverwriteTableConfig = new DynamoDBOperationConfig
            {
                OverrideTableName = _configuration.TableName,
                ConsistentRead = true
            };

            if (_configuration.NumberOfShards > 20)
            {
                throw new ArgumentOutOfRangeException(nameof(DynamoDbConfiguration.NumberOfShards), "Maximum number of shards is 20");
            }

            _outstandingTopicQueryContexts = new ConcurrentDictionary<string, TopicQueryContext>();
            _dispatchedTopicQueryContexts = new ConcurrentDictionary<string, TopicQueryContext>();

            _outstandingAllTopicsScanContext = new AllTopicsScanContext(_configuration.ScanConcurrency);
            _dispatchedAllTopicsScanContext = new AllTopicsScanContext(_configuration.ScanConcurrency);

            _instrumentationOptions = instrumentationOptions;
        }

        /// <summary>
        /// Initialises a new instance of the <see cref="DynamoDbOutbox"/> class. 
        /// </summary>
        /// <param name="context">An existing Dynamo Db Context</param>
        /// <param name="configuration">The Configuration from the context - the config is internal, so we can't grab the settings from it.</param>
        public DynamoDbOutbox(DynamoDBContext context, DynamoDbConfiguration configuration, TimeProvider timeProvider)
        {
            _context = context;
            _configuration = configuration;
            _timeProvider = timeProvider;
            _dynamoOverwriteTableConfig = new DynamoDBOperationConfig { OverrideTableName = _configuration.TableName };
            
            if (_configuration.NumberOfShards > 20)
            {
                throw new ArgumentOutOfRangeException(nameof(DynamoDbConfiguration.NumberOfShards), "Maximum number of shards is 20");
            }

            _outstandingTopicQueryContexts = new ConcurrentDictionary<string, TopicQueryContext>();
            _dispatchedTopicQueryContexts = new ConcurrentDictionary<string, TopicQueryContext>();

            _outstandingAllTopicsScanContext = new AllTopicsScanContext(_configuration.ScanConcurrency);
            _dispatchedAllTopicsScanContext = new AllTopicsScanContext(_configuration.ScanConcurrency);
        }

        /// <inheritdoc />
        /// <summary>
        /// Adds a message to the Outbox
        /// Sync over async
        /// </summary>       
        /// <param name="message">The message to be stored</param>
        /// <param name="requestContext">What is the context of this request; used to provide Span information to the call</param>
        /// <param name="outBoxTimeout">Timeout in milliseconds; -1 for default timeout</param>
        /// <param name="transactionProvider">Should we participate in a transaction</param>
        public void Add(
            Message message, 
            RequestContext? requestContext,
            int outBoxTimeout = -1, 
            IAmABoxTransactionProvider<TransactWriteItemsRequest>? transactionProvider = null
            )
        {
            AddAsync(message, requestContext, outBoxTimeout, transactionProvider).ConfigureAwait(ContinueOnCapturedContext).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Adds messages to the Outbox
        /// </summary>       
        /// <param name="messages">The messages to be stored</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>
        /// <param name="outBoxTimeout">Timeout in milliseconds; -1 for default timeout</param>
        /// <param name="transactionProvider">Should we participate in a transaction</param>
        public void Add(
            IEnumerable<Message> messages, 
            RequestContext? requestContext,
            int outBoxTimeout = -1, 
            IAmABoxTransactionProvider<TransactWriteItemsRequest>? transactionProvider = null
            )
        {
            foreach (var message in messages)
            {
                Add(message, requestContext, outBoxTimeout, transactionProvider);
            }
        }

        /// <summary>
        /// Adds a message to the Outbox
        /// </summary>
        /// <param name="message">The message to be stored</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>
        /// <param name="outBoxTimeout">Timeout in milliseconds; -1 for default timeout</param>
        /// <param name="transactionProvider">Should we participate in a transaction</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        public async Task AddAsync(
            Message message,
            RequestContext? requestContext,
            int outBoxTimeout = -1,
            IAmABoxTransactionProvider<TransactWriteItemsRequest>? transactionProvider = null,
            CancellationToken cancellationToken = default)
        {
            var dbAttributes = new Dictionary<string, string>()
            {
                {"db.operation.parameter.message.id", message.Id}
            };
            var span = Tracer?.CreateDbSpan(
                new BoxSpanInfo(DbSystem.Dynamodb, DYNAMO_DB_NAME, BoxDbOperation.Add, _configuration.TableName, dbAttributes: dbAttributes),
                requestContext?.Span,
                options: _instrumentationOptions);

            try
            {
                var shard = GetShardNumber();
                var expiresAt = GetExpirationTime();
                var messageToStore = new MessageItem(message, shard, expiresAt);

                if (transactionProvider != null)
                {
                    await AddToTransactionWrite(messageToStore, (DynamoDbUnitOfWork)transactionProvider);
                }
                else
                {
                    await WriteMessageToOutbox(cancellationToken, messageToStore);
                }
            }
            finally
            {
                Tracer?.EndSpan(span);
            }            
        }

        /// <summary>
        /// Adds messages to the Outbox
        /// </summary>
        /// <param name="messages">The messages to be stored</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>
        /// <param name="outBoxTimeout">Timeout in milliseconds; -1 for default timeout</param>
        /// <param name="transactionProvider"></param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        public async Task AddAsync(
            IEnumerable<Message> messages,
            RequestContext? requestContext,
            int outBoxTimeout = -1,
            IAmABoxTransactionProvider<TransactWriteItemsRequest>? transactionProvider = null,
            CancellationToken cancellationToken = default)
        {
            foreach (var message in messages)
            {
                await AddAsync(message, requestContext, outBoxTimeout, transactionProvider, cancellationToken);
            }
        }

        /// <summary>
        /// Delete messages from the Outbox
        /// Sync over async
        /// </summary>
        /// <param name="messageIds">The messages to delete</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>
        /// <param name="args">Additional parameters required to search if needed</param>
        public void Delete(Id[] messageIds, RequestContext? requestContext, Dictionary<string, object>? args = null)
        {
            DeleteAsync(messageIds, requestContext).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Delete messages from the Outbox
        /// </summary>
        /// <param name="messageIds">The messages to delete</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>
        /// <param name="args">Additional parameters required to search if needed</param>
        /// <param name="cancellationToken">Should the operation be cancelled</param>
        public async Task DeleteAsync(
            Id[] messageIds, 
            RequestContext? requestContext,
            Dictionary<string, object>? args = null,
            CancellationToken cancellationToken = default)
        {
            foreach (var messageId in messageIds)
            {
                await DeleteAsync(messageId, requestContext, args, cancellationToken);
            }
        }

        /// <summary>
        /// Returns messages that have been successfully dispatched. Eventually consistent.
        /// Sync over async
        /// </summary>
        /// <param name="dispatchedSince">How long ago was the message dispatched?</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>
        /// <param name="pageSize">How many messages returned at once?</param>
        /// <param name="pageNumber">Which page of the dispatched messages to return?</param>
        /// <param name="outboxTimeout"></param>
        /// <param name="args">Used to pass through the topic we are searching for messages in. Use Key: "Topic"</param>
        /// <returns>A list of dispatched messages</returns>
        public IEnumerable<Message> DispatchedMessages(
            TimeSpan dispatchedSince, 
            RequestContext requestContext,
            int pageSize = 100, 
            int pageNumber = 1, 
            int outboxTimeout = -1,
            Dictionary<string, object>? args = null)
        {
            return DispatchedMessagesAsync(dispatchedSince, requestContext, pageSize, pageNumber, outboxTimeout, args, CancellationToken.None)
                .GetAwaiter().GetResult();
        }

        /// <summary>
        /// Returns messages that have been successfully dispatched. Eventually consistent.
        /// </summary>
        /// <param name="dispatchedSince">How long ago was the message dispatched?</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>
        /// <param name="pageSize">How many messages returned at once?</param>
        /// <param name="pageNumber">Which page of the dispatched messages to return?</param>
        /// <param name="outboxTimeout"></param>
        /// <param name="args">Used to pass through the topic we are searching for messages in. Use Key: "Topic"</param>
        /// <param name="cancellationToken">Cancel the running operation</param>
        /// <returns>A list of dispatched messages</returns>
        /// <exception cref="ArgumentException"></exception>
        public async Task<IEnumerable<Message>> DispatchedMessagesAsync(
            TimeSpan dispatchedSince,
            RequestContext requestContext,
            int pageSize = 100,
            int pageNumber = 1,
            int outboxTimeout = -1,
            Dictionary<string, object>? args = null,
            CancellationToken cancellationToken = default)
        {
            var span = Tracer?.CreateDbSpan(
                new BoxSpanInfo(DbSystem.Dynamodb, DYNAMO_DB_NAME, BoxDbOperation.DispatchedMessages, _configuration.TableName),
                requestContext?.Span,
                options: _instrumentationOptions);

            try
            {
                IEnumerable<Message> result;
                if (args == null || !args.TryGetValue("Topic", out var topicArg))
                {
                    result = await DispatchedMessagesForAllTopicsAsync(dispatchedSince, pageSize, pageNumber, cancellationToken);
                }
                else
                {
                    result = await DispatchedMessagesForTopicAsync(dispatchedSince, pageSize, pageNumber, (string)topicArg, cancellationToken);
                }

                span?.AddTag("db.response.returned_rows", result.Count());
                return result;
            }
            finally
            {
                Tracer?.EndSpan(span);
            }
        }
        
        /// <summary>
        /// Returns messages that have been successfully dispatched. Eventually consistent. 
        /// </summary>
        /// <param name="dispatchedSince">How many hours back to look</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>
        /// <param name="pageSize">The number of results to return. Only returns this number of results</param>
        /// <param name="cancellationToken">How to cancel</param>
        /// <returns></returns>
        public async Task<IEnumerable<Message>> DispatchedMessagesAsync(
            TimeSpan dispatchedSince, 
            RequestContext requestContext,
            int pageSize = 100,
            CancellationToken cancellationToken = default
            )
        {
            return await DispatchedMessagesAsync(dispatchedSince, requestContext, pageSize: pageSize, pageNumber: 1, outboxTimeout: -1, args: null, cancellationToken: cancellationToken);
        }

        /// <summary>
        ///  Finds a message with the specified identifier.
        ///  Sync over async
        /// </summary>
        /// <param name="messageId">The identifier.</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>
        /// <param name="outBoxTimeout">Timeout in milliseconds; -1 for default timeout</param>
        /// <param name="args"></param>
        /// <returns><see cref="T:Paramore.Brighter.Message" /></returns>
        public Message Get(Id messageId, RequestContext requestContext, int outBoxTimeout = -1, Dictionary<string, object>? args = null)
        {
            return GetAsync(messageId, requestContext, outBoxTimeout, args).ConfigureAwait(ContinueOnCapturedContext).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Finds a message with the specified identifier.
        /// </summary>
        /// <param name="messageId">The identifier.</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>
        /// <param name="outBoxTimeout">Timeout in milliseconds; -1 for default timeout</param>
        /// <param name="args">For outboxes that require additional parameters such as topic, provide an optional arg</param>
        /// <param name="cancellationToken"></param>
        /// <returns><see cref="T:Paramore.Brighter.Message" /></returns>
        public async Task<Message> GetAsync(
            Id messageId,
            RequestContext requestContext,
            int outBoxTimeout = -1,
            Dictionary<string, object>? args = null,
            CancellationToken cancellationToken = default)
        {
            var dbAttributes = new Dictionary<string, string>()
            {
                {"db.operation.parameter.message.id", messageId}
            };
            var span = Tracer?.CreateDbSpan(
                new BoxSpanInfo(DbSystem.Dynamodb, DYNAMO_DB_NAME, BoxDbOperation.Get, _configuration.TableName, dbAttributes: dbAttributes),
                requestContext?.Span,
                options: _instrumentationOptions);

            try
            {
                var messageItem = await _context.LoadAsync<MessageItem>(messageId.Value, _dynamoOverwriteTableConfig, cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);
                return messageItem?.ConvertToMessage() ?? new Message();
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
        /// <param name="args"></param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        public async Task MarkDispatchedAsync(
            Id id,
            RequestContext requestContext,
            DateTimeOffset? dispatchedAt = null,
            Dictionary<string, object>? args = null,
            CancellationToken cancellationToken = default)
        {
            var dbAttributes = new Dictionary<string, string>()
            {
                {"db.operation.parameter.message.id", id}
            };
            var span = Tracer?.CreateDbSpan(
                new BoxSpanInfo(DbSystem.Dynamodb, DYNAMO_DB_NAME, BoxDbOperation.MarkDispatched, _configuration.TableName, dbAttributes: dbAttributes),
                requestContext?.Span,
                options: _instrumentationOptions);

            try
            {
                var message = await _context.LoadAsync<MessageItem>(id.Value, _dynamoOverwriteTableConfig, cancellationToken)
                    .ConfigureAwait(ContinueOnCapturedContext);
                MarkMessageDispatched(dispatchedAt ?? _timeProvider.GetUtcNow(), message);

                await _context.SaveAsync(
                    message,
                    _dynamoOverwriteTableConfig,
                    cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
            }
            finally
            {
                Tracer?.EndSpan(span);
            }
        }

        /// <summary>
        /// Marks a set of messages as dispatched in the Outbox
        /// </summary>
        /// <param name="ids">The ides of the messages to mark as dispatched</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>
        /// <param name="dispatchedAt">What time were the messages dispatched at? Defaults to Utc.Now</param>
        /// <param name="args">What is the topic of the message</param>
        /// <param name="cancellationToken">Cancel an ongoing operation</param>
        public async Task MarkDispatchedAsync(
            IEnumerable<Id> ids,
            RequestContext requestContext,
            DateTimeOffset? dispatchedAt = null,
            Dictionary<string, object>? args = null,
            CancellationToken cancellationToken = default)
        {
            foreach(var messageId in ids)
            {
                await MarkDispatchedAsync(messageId, requestContext, dispatchedAt, args, cancellationToken);
            }
        }

        /// <summary>
        /// Update a message to show it is dispatched
        /// </summary>
        /// <param name="id">The id of the message to update</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>
        /// <param name="dispatchedAt">When was the message dispatched, defaults to UTC now</param>
        /// <param name="args"></param>
        public void MarkDispatched(Id id, RequestContext requestContext, DateTimeOffset? dispatchedAt = null, Dictionary<string, object>? args = null)
        {
            MarkDispatchedAsync(id, requestContext, dispatchedAt, args)
                .ConfigureAwait(ContinueOnCapturedContext)
                .GetAwaiter()
                .GetResult();
        }

        private static void MarkMessageDispatched(DateTimeOffset dispatchedAt, MessageItem message)
        {
            message.DeliveryTime = dispatchedAt.Ticks;
            message.DeliveredAt = dispatchedAt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

            // Set the outstanding created time to null to remove the attribute
            // from the item in dynamo
            message.OutstandingCreatedTime = null;
        }

        /// <summary>
        /// Returns messages that have yet to be dispatched
        /// Sync over async
        /// </summary>
        /// <param name="dispatchedSince">How long ago as the message sent?</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>        
        /// <param name="pageSize">How many messages to return at once?</param>
        /// <param name="pageNumber">Which page number of messages</param>
        /// <param name="trippedTopics">Collection of tripped topics</param>
        /// <param name="args"></param>
        /// <returns>A list of messages that are outstanding for dispatch</returns>
        public IEnumerable<Message> OutstandingMessages(
            TimeSpan dispatchedSince, 
            RequestContext? requestContext,
            int pageSize = 100, 
            int pageNumber = 1,
            IEnumerable<RoutingKey>? trippedTopics = null,
            Dictionary<string, object>? args = null)
        {
            return OutstandingMessagesAsync(dispatchedSince, requestContext, pageSize, pageNumber, args: args)
                .GetAwaiter()
                .GetResult();
        }

        /// <summary>
        /// Returns messages that have yet to be dispatched
        /// </summary>
        /// <param name="dispatchedSince">How long ago as the message sent?</param>
        /// <param name="requestContext"></param>
        /// <param name="pageSize">How many messages to return at once?</param>
        /// <param name="pageNumber">Which page number of messages</param>
        /// <param name="trippedTopics">Collection of tripped topics</param>
        /// <param name="args"></param>
        /// <param name="cancellationToken">Async Cancellation Token</param>
        /// <returns>A list of messages that are outstanding for dispatch</returns>
        public async Task<IEnumerable<Message>> OutstandingMessagesAsync(
            TimeSpan dispatchedSince,
            RequestContext? requestContext,
            int pageSize = 100,
            int pageNumber = 1,
            IEnumerable<RoutingKey>? trippedTopics = null,
            Dictionary<string, object>? args = null,
            CancellationToken cancellationToken = default)
        {
            var span = Tracer?.CreateDbSpan(
                new BoxSpanInfo(DbSystem.Dynamodb, DYNAMO_DB_NAME, BoxDbOperation.OutStandingMessages, _configuration.TableName),
                requestContext?.Span,
                options: _instrumentationOptions);

            try
            {
                IEnumerable<Message> result;
                if (args == null || !args.TryGetValue("Topic", out var topicArg))
                {
                    result = await OutstandingMessagesForAllTopicsAsync(dispatchedSince, pageSize, pageNumber, cancellationToken);
                }
                else
                {
                    result = await OutstandingMessagesForTopicAsync(dispatchedSince, pageSize, pageNumber, (string)topicArg, cancellationToken);
                }

                span?.AddTag("db.response.returned_rows", result.Count());
                return result;
            }
            finally
            {
                Tracer?.EndSpan(span);
            }
        }

        private async Task DeleteAsync(
            Id messageId,
            RequestContext? requestContext,
            Dictionary<string, object>? args,
            CancellationToken cancellationToken)
        {
            var dbAttributes = new Dictionary<string, string>()
            {
                {"db.operation.parameter.message.id", messageId}
            };
            var span = Tracer?.CreateDbSpan(
                new BoxSpanInfo(DbSystem.Dynamodb, DYNAMO_DB_NAME, BoxDbOperation.Delete, _configuration.TableName, dbAttributes: dbAttributes),
                requestContext?.Span,
                options: _instrumentationOptions);

            try
            {
                await _context.DeleteAsync<MessageItem>(messageId.Value, _dynamoOverwriteTableConfig, cancellationToken);
            }
            finally
            {
                Tracer?.EndSpan(span);
            }
        }

        private async Task<IEnumerable<Message>> OutstandingMessagesForAllTopicsAsync(TimeSpan dispatchedSince, int pageSize, int pageNumber, CancellationToken cancellationToken)
        {
            // Only allow one outstanding messages scan at a time to ensure consistency of pagination tokens
            await _outstandingAllTopicsScanContext.Lock(cancellationToken);
            try
            {
                var olderThan = _timeProvider.GetUtcNow() - dispatchedSince;

                // Validate that this is a query for a page we can actually retrieve
                if (pageNumber != 1 && _outstandingAllTopicsScanContext.NextPage != pageNumber)
                {
                    var nextPageNumber = _outstandingAllTopicsScanContext.NextPage;
                    var errorMessage = $"Unable to query page {pageNumber} of outstanding messages for all topics - next available page is page {nextPageNumber}";
                    throw new ArgumentOutOfRangeException(nameof(pageNumber), errorMessage);
                }

                // Spin off requests to scan each segment
                var tasks = new List<Task<List<MessageItem>>>();
                var segmentPageSize = pageSize / _configuration.ScanConcurrency;
                for (var segmentNumber = 0; segmentNumber < _configuration.ScanConcurrency; segmentNumber++)
                {
                    tasks.Add(ScanOutstandingIndexSegment(olderThan, segmentPageSize, pageNumber, segmentNumber, cancellationToken));
                }

                await Task.WhenAll(tasks);

                // Set the next page number based on the pagination tokens for the different segments
                _outstandingAllTopicsScanContext.SetNextPage();

                var allMessages = tasks.SelectMany(t => t.Result);
                return allMessages
                    .OrderBy(m => m.OutstandingCreatedTime)
                    .Select(m => m.ConvertToMessage());
            }
            finally
            {
                _outstandingAllTopicsScanContext.Release();
            }
        }

        private async Task<List<MessageItem>> ScanOutstandingIndexSegment(DateTimeOffset olderThan, 
            int pageSize, 
            int pageNumber, 
            int segmentNumber,
            CancellationToken cancellationToken)
        {
            string? paginationToken = null;
            if (pageNumber != 1 && paginationToken == null)
            {
                // It may be that this segment is done but other segments have more results
                return new List<MessageItem>();
            }
            else if (pageNumber != 1)
            {
                paginationToken = _outstandingAllTopicsScanContext.GetPagingToken(segmentNumber);
            }
            
            var segmentMessages = new List<MessageItem>();
            do
            {
                var scanFilter = new ScanFilter();
                scanFilter.AddCondition("OutstandingCreatedTime", ScanOperator.LessThanOrEqual,
                    new List<AttributeValue>
                    {
                        new AttributeValue()
                        {
                            N = olderThan.Ticks.ToString()
                        }
                    });
                var scanConfig = new ScanOperationConfig
                {
                    IndexName = _configuration.OutstandingAllTopicsIndexName,
                    ConsistentRead = false,
                    Filter = scanFilter,
                    Select = SelectValues.AllProjectedAttributes,
                    Limit = pageSize - segmentMessages.Count,
                    PaginationToken = paginationToken,
                    Segment = segmentNumber,
                    TotalSegments = _configuration.ScanConcurrency
                };
                var scan = _context.FromScanAsync<MessageItem>(scanConfig, _dynamoOverwriteTableConfig);

                segmentMessages.AddRange(await scan.GetNextSetAsync(cancellationToken));

                paginationToken = scan.PaginationToken;
            } while (paginationToken != null && segmentMessages.Count < pageSize);

            // If there are more results, store the context for retrieving the next page
            if (paginationToken != null)
            {
                _outstandingAllTopicsScanContext.SetPagingToken(segmentNumber, paginationToken);
            }
            else
            {
                _outstandingAllTopicsScanContext.SetPagingToken(segmentNumber, null);
            }

            return segmentMessages;
        }

        private async Task<IEnumerable<Message>> OutstandingMessagesForTopicAsync(TimeSpan dispatchedSince, int pageSize, int pageNumber,
            string topicName, CancellationToken cancellationToken)
        {
            var context = _outstandingTopicQueryContexts.GetOrAdd(topicName, new TopicQueryContext());

            await context.Lock(cancellationToken);
            try
            {
                var olderThan = _timeProvider.GetUtcNow() - dispatchedSince;

                // Validate that this is a query for a page we can actually retrieve
                if (pageNumber != 1 && context.NextPage != pageNumber)
                {
                    var nextPageNumber = context.NextPage;
                    var errorMessage = $"Unable to query page {pageNumber} of outstanding messages for topic {topicName} - next available page is page {nextPageNumber}";
                    throw new ArgumentOutOfRangeException(nameof(pageNumber), errorMessage);
                }

                // Query as much as possible up to the max page (batch) size
                string? paginationToken = null;
                int initialShardNumber = 0;
                if (pageNumber != 1)
                {
                    paginationToken = _outstandingTopicQueryContexts[topicName]!.LastEvaluatedKey;
                    initialShardNumber = _outstandingTopicQueryContexts[topicName]!.ShardNumber;
                }

                var queryResult = await PageOutstandingMessagesToBatchSizeAsync(topicName, olderThan, pageSize, initialShardNumber, paginationToken, cancellationToken);

                // Store the progress for this topic if there are further pages
                if (!queryResult.QueryComplete)
                {
                    context.SetPaginationState(pageNumber + 1, queryResult.ShardNumber, queryResult.PaginationToken);
                }
                else
                {
                    context.Reset();
                }

                return queryResult.Messages.Select(msg => msg.ConvertToMessage());
            }
            finally
            {
                context.Release();
            }
        }

        private Task<TransactWriteItemsRequest?> AddToTransactionWrite(MessageItem messageToStore, DynamoDbUnitOfWork dynamoDbUnitOfWork)
        {
            var tcs = new TaskCompletionSource<TransactWriteItemsRequest?>();
            var attributes = _context.ToDocument(messageToStore, _dynamoOverwriteTableConfig).ToAttributeMap();
            
            var transaction = dynamoDbUnitOfWork.GetTransaction();
            transaction.TransactItems.Add(new TransactWriteItem{Put = new Put{TableName = _configuration.TableName, Item = attributes}});
            tcs.SetResult(transaction);
            return tcs.Task;
        }

        private async Task<IEnumerable<Message>> DispatchedMessagesForTopicAsync(
            TimeSpan dispatchedSince,
            int pageSize,
            int pageNumber,
            string topicName,
            CancellationToken cancellationToken)
        {
            var context = _dispatchedTopicQueryContexts.GetOrAdd(topicName, new TopicQueryContext());

            await context.Lock(cancellationToken);
            try
            {
                var sinceTime = _timeProvider.GetUtcNow() - dispatchedSince;

                // Validate that this is a query for a page we can actually retrieve
                if (pageNumber != 1 && pageNumber != context.NextPage)
                {
                    var nextPageNumber = context.NextPage;
                    var errorMessage = $"Unable to query page {pageNumber} of dispatched messages for topic {topicName} - next available page is page {nextPageNumber}";
                    throw new ArgumentOutOfRangeException(nameof(pageNumber), errorMessage);
                }

                // Query as much as possible up to the max page (batch) size
                var paginationToken = pageNumber == 1 ? null : context.LastEvaluatedKey;
                var queryResult = await PageDispatchedMessagesToBatchSizeAsync(topicName, sinceTime, pageSize, paginationToken, cancellationToken);

                // Store the progress for this topic if there are further pages
                if (!queryResult.QueryComplete)
                {
                    context.SetPaginationState(pageNumber + 1, 0, queryResult.PaginationToken);
                }
                else
                {
                    context.Reset();
                }

                return queryResult.Messages.Select(msg => msg.ConvertToMessage());
            }
            finally
            {
                context.Release();
            }
        }

        private async Task<IEnumerable<Message>> DispatchedMessagesForAllTopicsAsync(
            TimeSpan minimumAge,
            int pageSize,
            int pageNumber,
            CancellationToken cancellationToken)
        {
            // Only allow one dispatched messages scan at a time to ensure consistency of pagination tokens
            await _dispatchedAllTopicsScanContext.Lock(cancellationToken);
            try
            {
                var dispatchedBefore = _timeProvider.GetUtcNow() - minimumAge;

                // Validate that this is a query for a page we can actually retrieve
                if (pageNumber != 1 && _dispatchedAllTopicsScanContext.NextPage != pageNumber)
                {
                    var nextPageNumber = _dispatchedAllTopicsScanContext.NextPage;
                    var errorMessage = $"Unable to query page {pageNumber} of dispatched messages for all topics - next available page is page {nextPageNumber}";
                    throw new ArgumentOutOfRangeException(nameof(pageNumber), errorMessage);
                }

                // Spin off requests to scan each segment
                var tasks = new List<Task<List<MessageItem>>>();
                var segmentPageSize = pageSize / _configuration.ScanConcurrency;
                for (var segmentNumber = 0; segmentNumber < _configuration.ScanConcurrency; segmentNumber++)
                {
                    tasks.Add(ScanDispatchedIndexSegment(dispatchedBefore, segmentPageSize, pageNumber, segmentNumber, cancellationToken));
                }

                await Task.WhenAll(tasks);

                // Set the next page number based on the pagination tokens for the different segments
                _dispatchedAllTopicsScanContext.SetNextPage();

                var allMessages = tasks.SelectMany(t => t.Result);
                return allMessages
                    .OrderBy(m => m.DeliveryTime)
                    .Select(m => m.ConvertToMessage());
            }
            finally
            {
                _dispatchedAllTopicsScanContext.Release();
            }
        }

        private async Task<List<MessageItem>> ScanDispatchedIndexSegment(DateTimeOffset dispatchedBefore,
            int pageSize,
            int pageNumber,
            int segmentNumber,
            CancellationToken cancellationToken)
        {
            string? paginationToken = null;
            if (pageNumber != 1 && paginationToken == null)
            {
                // It may be that this segment is done but other segments have more results
                return new List<MessageItem>();
            }
            else if (pageNumber != 1)
            {
                paginationToken = _dispatchedAllTopicsScanContext.GetPagingToken(segmentNumber);
            }

            var segmentMessages = new List<MessageItem>();
            do
            {
                var scanFilter = new ScanFilter();
                scanFilter.AddCondition("DeliveryTime", ScanOperator.LessThanOrEqual,
                    new List<AttributeValue>
                    {
                        new AttributeValue()
                        {
                            N = dispatchedBefore.Ticks.ToString()
                        }
                    });
                var scanConfig = new ScanOperationConfig
                {
                    IndexName = _configuration.DeliveredAllTopicsIndexName,
                    ConsistentRead = false,
                    Filter = scanFilter,
                    Select = SelectValues.AllProjectedAttributes,
                    Limit = pageSize - segmentMessages.Count,
                    PaginationToken = paginationToken,
                    Segment = segmentNumber,
                    TotalSegments = _configuration.ScanConcurrency
                };
                var scan = _context.FromScanAsync<MessageItem>(scanConfig, _dynamoOverwriteTableConfig);

                segmentMessages.AddRange(await scan.GetNextSetAsync(cancellationToken));

                paginationToken = scan.PaginationToken;
            } while (paginationToken != null && segmentMessages.Count < pageSize);

            // If there are more results, store the context for retrieving the next page
            if (paginationToken != null)
            {
                _dispatchedAllTopicsScanContext.SetPagingToken(segmentNumber, paginationToken);
            }
            else
            {
                _dispatchedAllTopicsScanContext.SetPagingToken(segmentNumber, null);
            }

            return segmentMessages;
        }

        private async Task<OutstandingMessagesQueryResult> PageOutstandingMessagesToBatchSizeAsync(
            string topicName, 
            DateTimeOffset olderThan, 
            int batchSize,
            int initialShardNumber, 
            string? initialPaginationToken,
            CancellationToken cancellationToken)
        {
            var numShards = _configuration.NumberOfShards <= 1 ? 1 : _configuration.NumberOfShards;
            var results = new List<MessageItem>();

            var paginationToken = initialPaginationToken;
            var isDone = false;
            var shard = initialShardNumber;
            while (shard < numShards && results.Count < batchSize)
            {
                do
                {
                    var queryConfig = new QueryOperationConfig
                    {
                        IndexName = _configuration.OutstandingIndexName,
                        KeyExpression = new KeyTopicOutstandingCreatedTimeExpression().Generate(topicName, olderThan, shard),
                        Limit = batchSize - results.Count,
                        PaginationToken = paginationToken,
                        ConsistentRead = false
                    };

                    var asyncSearch = _context.FromQueryAsync<MessageItem>(queryConfig, _dynamoOverwriteTableConfig);
                    results.AddRange(await asyncSearch.GetNextSetAsync(cancellationToken));

                    paginationToken = asyncSearch.PaginationToken;
                    isDone = asyncSearch.IsDone;
                } while (results.Count < batchSize && !isDone);

                // Only move on to the next shard if we still have room to fill up in the batch
                if (results.Count < batchSize)
                {
                    shard++;
                }
            }

            var nextShardNumber = 0;
            var queryComplete = true;
            if (!isDone)
            {
                // We are part way through a shard
                // continue on the current shard in the next batch
                nextShardNumber = shard;
                queryComplete = false;
            }
            else if (shard < numShards)
            {
                // We are exactly at the end of a shard
                // continue on the next shard in the next batch
                nextShardNumber = shard + 1;
                queryComplete = false;
            }

            return new OutstandingMessagesQueryResult(results, nextShardNumber, paginationToken, queryComplete);
        }

        private async Task<DispatchedMessagesQueryResult> PageDispatchedMessagesToBatchSizeAsync(
            string topicName,
            DateTimeOffset sinceTime,
            int batchSize,
            string? initialPaginationToken,
            CancellationToken cancellationToken)
        {
            var results = new List<MessageItem>();
            var keyExpression = new KeyTopicDeliveredTimeExpression().Generate(topicName, sinceTime);
            var paginationToken = initialPaginationToken;
            var isDone = false;
            do
            {
                var queryConfig = new QueryOperationConfig
                {
                    IndexName = _configuration.DeliveredIndexName,
                    KeyExpression = keyExpression,
                    Limit = batchSize - results.Count,
                    PaginationToken = paginationToken
                };

                var asyncSearch = _context.FromQueryAsync<MessageItem>(queryConfig, _dynamoOverwriteTableConfig);
                results.AddRange(await asyncSearch.GetNextSetAsync(cancellationToken));

                paginationToken = asyncSearch.PaginationToken;
                isDone = asyncSearch.IsDone;
            } while (results.Count < batchSize && !isDone);

            return new DispatchedMessagesQueryResult(results, paginationToken, isDone);
        }
        
        private async Task WriteMessageToOutbox(CancellationToken cancellationToken, MessageItem messageToStore)
        {
            await _context.SaveAsync(
                    messageToStore,
                    _dynamoOverwriteTableConfig,
                    cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);
        }

        private int GetShardNumber()
        {
            if (_configuration.NumberOfShards <= 1)
                return 0;

            //The range is inclusive of 0 but exclusive of NumberOfShards i.e. 0, 4 produces values in range 0-3
            return _random.Next(0, _configuration.NumberOfShards);
        }

        private long? GetExpirationTime()
        {
            if (_configuration.TimeToLive.HasValue)
            {
                return _timeProvider.GetUtcNow().Add(_configuration.TimeToLive.Value).ToUnixTimeSeconds();
            }

            return null;
        }
    }
}
