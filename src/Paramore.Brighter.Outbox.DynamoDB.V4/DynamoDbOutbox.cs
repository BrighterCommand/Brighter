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
using Paramore.Brighter.DynamoDb.V4;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Outbox.DynamoDB.V4;

public class DynamoDbOutbox :
    IAmAnOutboxSync<Message, TransactWriteItemsRequest>,
    IAmAnOutboxAsync<Message, TransactWriteItemsRequest>
{
    private readonly DynamoDbConfiguration _configuration;
    private readonly DynamoDBContext _context;
    private readonly LoadConfig _loadConfig;
    private readonly SaveConfig _saveConfig;
    private readonly DeleteConfig _deleteConfig;
    private readonly FromQueryConfig _fromQueryConfig;
    private readonly ToDocumentConfig _toDocumentConfig;
    private readonly Random _random = new Random();
    private readonly TimeProvider _timeProvider;

    private readonly ConcurrentDictionary<string, OutstandingTopicQueryContext?> _outstandingTopicQueryContexts;
    private readonly ConcurrentDictionary<string, DispatchedTopicQueryContext> _dispatchedTopicQueryContexts;

    private readonly ConcurrentDictionary<string, byte> _topicNames;

    private OutstandingAllTopicsQueryContext? _outstandingAllTopicsQueryContext;
    private DispatchedAllTopicsQueryContext? _dispatchedAllTopicsQueryContext;

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
        _context = new DynamoDBContextBuilder().WithDynamoDBClient(() =>  client).Build();
        _timeProvider = timeProvider ?? TimeProvider.System;
        new DynamoDBOperationConfig
        {
            OverrideTableName = _configuration.TableName,
            ConsistentRead = true
        };

        _loadConfig = new LoadConfig
        {
            OverrideTableName = _configuration.TableName,
            ConsistentRead = true
        };
        
        _saveConfig = new SaveConfig { OverrideTableName = _configuration.TableName };
        _deleteConfig = new DeleteConfig { OverrideTableName = _configuration.TableName, };
        _fromQueryConfig = new FromQueryConfig { OverrideTableName = _configuration.TableName };
        _toDocumentConfig = new ToDocumentConfig { OverrideTableName = _configuration.TableName, };


        if (_configuration.NumberOfShards > 20)
        {
            throw new ArgumentOutOfRangeException(nameof(DynamoDbConfiguration.NumberOfShards), "Maximum number of shards is 20");
        }

        _outstandingTopicQueryContexts = new ConcurrentDictionary<string, OutstandingTopicQueryContext?>();
        _dispatchedTopicQueryContexts = new ConcurrentDictionary<string, DispatchedTopicQueryContext>();
        _topicNames = new ConcurrentDictionary<string, byte>();

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
        new DynamoDBOperationConfig { OverrideTableName = _configuration.TableName };
        _loadConfig = new LoadConfig { OverrideTableName = _configuration.TableName };
        _saveConfig = new SaveConfig { OverrideTableName = _configuration.TableName };
        _deleteConfig = new DeleteConfig { OverrideTableName = _configuration.TableName, };
        _fromQueryConfig = new FromQueryConfig { OverrideTableName = _configuration.TableName };
        _toDocumentConfig = new ToDocumentConfig { OverrideTableName = _configuration.TableName, };

            
        if (_configuration.NumberOfShards > 20)
        {
            throw new ArgumentOutOfRangeException(nameof(DynamoDbConfiguration.NumberOfShards), "Maximum number of shards is 20");
        }

        _outstandingTopicQueryContexts = new ConcurrentDictionary<string, OutstandingTopicQueryContext?>();
        _dispatchedTopicQueryContexts = new ConcurrentDictionary<string, DispatchedTopicQueryContext>();
        _topicNames = new ConcurrentDictionary<string, byte>();
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

            // Store the name of the topic as a key in a concurrent dictionary to ensure uniqueness & thread safety
            _topicNames.TryAdd(message.Header.Topic, 0);

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
            var messageItem = await _context.LoadAsync<MessageItem>(messageId.Value, _loadConfig, cancellationToken)
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
            var message = await _context.LoadAsync<MessageItem>(id.Value, _loadConfig, cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);
            MarkMessageDispatched(dispatchedAt ?? _timeProvider.GetUtcNow(), message);

            await _context.SaveAsync(
                message,
                _saveConfig,
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
        return OutstandingMessagesAsync(dispatchedSince, requestContext, pageSize, pageNumber, trippedTopics, args)
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
            await _context.DeleteAsync<MessageItem>(messageId.Value, _deleteConfig, cancellationToken);
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    private async Task<IEnumerable<Message>> OutstandingMessagesForAllTopicsAsync(TimeSpan dispatchedSince, int pageSize, int pageNumber, CancellationToken cancellationToken)
    {
        var olderThan = _timeProvider.GetUtcNow() - dispatchedSince;

        // Validate that this is a query for a page we can actually retrieve
        if (pageNumber != 1 && _outstandingAllTopicsQueryContext?.NextPage != pageNumber)
        {
            var nextPageNumber = _outstandingAllTopicsQueryContext?.NextPage ?? 1;
            var errorMessage = $"Unable to query page {pageNumber} of outstanding messages for all topics - next available page is page {nextPageNumber}";
            throw new ArgumentOutOfRangeException(nameof(pageNumber), errorMessage);
        }

        // Get the list of topic names we need to query over,
        // the current paging token if there is one & this isn't the first page,
        // and the current shard to be paged over for the current topic
        List<string> topics;
        string? paginationToken;
        int currentShard;
        if (pageNumber == 1)
        {
            topics = _topicNames.Keys.ToList();
            paginationToken = null;
            currentShard = 0;
        }
        else
        {
            topics = _outstandingAllTopicsQueryContext!.RemainingTopics;
            paginationToken = _outstandingAllTopicsQueryContext.LastEvaluatedKey;
            currentShard = _outstandingAllTopicsQueryContext.ShardNumber;
        }

        // Iterate over topics and their associated shards until we reach the batch size
        var results = new List<MessageItem>();
        var currentTopicIndex = 0;
        while (results.Count < pageSize && currentTopicIndex < topics.Count)
        {
            var remainingBatchSize = pageSize - results.Count;
            var queryResult = await PageOutstandingMessagesToBatchSizeAsync(
                topics[currentTopicIndex], 
                olderThan, 
                remainingBatchSize, 
                currentShard, 
                paginationToken, 
                cancellationToken);

            results.AddRange(queryResult.Messages);

            if (queryResult.QueryComplete)
            {
                currentTopicIndex++;
                paginationToken = null;
                currentShard = 0;
            }
            else
            {
                paginationToken = queryResult.PaginationToken;
                currentShard = queryResult.ShardNumber;
            }
        }

        // Store the progress for the "all topics" query if there are further pages
        if (currentTopicIndex < topics.Count)
        {
            var remainingTopics = topics.GetRange(currentTopicIndex, topics.Count - currentTopicIndex);
            _outstandingAllTopicsQueryContext = new OutstandingAllTopicsQueryContext(pageNumber + 1, paginationToken!, currentShard, remainingTopics);
        }
        else
        {
            _outstandingAllTopicsQueryContext = null;
        }

        return results.Select(msg => msg.ConvertToMessage());
    }

    private async Task<IEnumerable<Message>> OutstandingMessagesForTopicAsync(TimeSpan dispatchedSince, int pageSize, int pageNumber,
        string topicName, CancellationToken cancellationToken)
    {
        var olderThan = _timeProvider.GetUtcNow() - dispatchedSince;

        // Validate that this is a query for a page we can actually retrieve
        if (pageNumber != 1)
        {
            if (!_outstandingTopicQueryContexts.TryGetValue(topicName, out OutstandingTopicQueryContext? context))
            {
                var errorMessage = $"Unable to query page {pageNumber} of outstanding messages for topic {topicName} - next available page is page 1";
                throw new ArgumentOutOfRangeException(nameof(pageNumber), errorMessage);
            }

            if (context?.NextPage != pageNumber)
            {
                var nextPageNumber = _dispatchedTopicQueryContexts[topicName]?.NextPage ?? 1;
                var errorMessage = $"Unable to query page {pageNumber} of outstanding messages for topic {topicName} - next available page is page {nextPageNumber}";
                throw new ArgumentOutOfRangeException(nameof(pageNumber), errorMessage);
            }
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
            _outstandingTopicQueryContexts.AddOrUpdate(topicName,
                new OutstandingTopicQueryContext(pageNumber + 1, queryResult.ShardNumber, queryResult.PaginationToken),
                (_, _) => new OutstandingTopicQueryContext(pageNumber + 1, queryResult.ShardNumber, queryResult.PaginationToken));
        }
        else
        {
            _outstandingTopicQueryContexts.TryRemove(topicName, out _);
        }

        return queryResult.Messages.Select(msg => msg.ConvertToMessage());
    }

    private Task<TransactWriteItemsRequest> AddToTransactionWrite(MessageItem messageToStore, DynamoDbUnitOfWork dynamoDbUnitOfWork)
    {
        var tcs = new TaskCompletionSource<TransactWriteItemsRequest>();
        var attributes = _context.ToDocument(messageToStore, _toDocumentConfig).ToAttributeMap();
            
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
        var sinceTime = _timeProvider.GetUtcNow() - dispatchedSince;

        // Validate that this is a query for a page we can actually retrieve
        if (pageNumber != 1)
        {
            if (!_dispatchedTopicQueryContexts.TryGetValue(topicName, out var context))
            {
                var errorMessage = $"Unable to query page {pageNumber} of dispatched messages for topic {topicName} - next available page is page 1";
                throw new ArgumentOutOfRangeException(nameof(pageNumber), errorMessage);
            }

            if (context?.NextPage != pageNumber)
            {
                var nextPageNumber = _dispatchedTopicQueryContexts[topicName]?.NextPage ?? 1;
                var errorMessage = $"Unable to query page {pageNumber} of dispatched messages for topic {topicName} - next available page is page {nextPageNumber}";
                throw new ArgumentOutOfRangeException(nameof(pageNumber), errorMessage);
            }
        }

        // Query as much as possible up to the max page (batch) size
        var paginationToken = pageNumber == 1 ? null : _dispatchedTopicQueryContexts[topicName].LastEvaluatedKey;
        var queryResult = await PageDispatchedMessagesToBatchSizeAsync(topicName, sinceTime, pageSize, paginationToken, cancellationToken);

        // Store the progress for this topic if there are further pages
        if (!queryResult.QueryComplete)
        {
            _dispatchedTopicQueryContexts.AddOrUpdate(topicName,
                new DispatchedTopicQueryContext(pageNumber + 1, queryResult.PaginationToken),
                (_, _) => new DispatchedTopicQueryContext(pageNumber + 1, queryResult.PaginationToken));
        }
        else
        {
            _dispatchedTopicQueryContexts.TryRemove(topicName, out _);
        }

        return queryResult.Messages.Select(msg => msg.ConvertToMessage());
    }

    private async Task<IEnumerable<Message>> DispatchedMessagesForAllTopicsAsync(
        TimeSpan dispatchedSince,
        int pageSize,
        int pageNumber,
        CancellationToken cancellationToken)
    {
        var sinceTime = _timeProvider.GetUtcNow() - dispatchedSince;

        // Validate that this is a query for a page we can actually retrieve
        if (pageNumber != 1 && _dispatchedAllTopicsQueryContext?.NextPage != pageNumber)
        {
            var nextPageNumber = _dispatchedAllTopicsQueryContext?.NextPage ?? 1;
            var errorMessage = $"Unable to query page {pageNumber} of dispatched messages for all topics - next available page is page {nextPageNumber}";
            throw new ArgumentOutOfRangeException(nameof(pageNumber), errorMessage);
        }

        // Get the list of topic names we need to query over, and the current paging token if there is one & this isn't the first page
        List<string> topics;
        string? paginationToken;
        if (pageNumber == 1)
        {
            topics = _topicNames.Keys.ToList();
            paginationToken = null;
        }
        else
        {
            topics = _dispatchedAllTopicsQueryContext!.RemainingTopics;
            paginationToken = _dispatchedAllTopicsQueryContext.LastEvaluatedKey;
        }

        // Iterate over topic until we reach the batch size
        var results = new List<MessageItem>();
        var currentTopicIndex = 0;
        while (results.Count < pageSize && currentTopicIndex < topics.Count)
        {
            var remainingBatchSize = pageSize - results.Count;
            var queryResult = await PageDispatchedMessagesToBatchSizeAsync(
                topics[currentTopicIndex],
                sinceTime,
                remainingBatchSize,
                paginationToken,
                cancellationToken);

            results.AddRange(queryResult.Messages);

            if (queryResult.QueryComplete)
            {
                currentTopicIndex++;
                paginationToken = null;
            }
            else
            {
                paginationToken = queryResult.PaginationToken;
            }
        }

        // Store the progress for the "all topics" query if there are further pages
        if (currentTopicIndex < topics.Count)
        {
            var outstandingTopics = topics.GetRange(currentTopicIndex, topics.Count - currentTopicIndex);
            _dispatchedAllTopicsQueryContext = new DispatchedAllTopicsQueryContext(pageNumber + 1, paginationToken, outstandingTopics);
        }
        else
        {
            _dispatchedAllTopicsQueryContext = null;
        }

        return results.Select(msg => msg.ConvertToMessage());
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

                var asyncSearch = _context.FromQueryAsync<MessageItem>(queryConfig, _fromQueryConfig);
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

            var asyncSearch = _context.FromQueryAsync<MessageItem>(queryConfig, _fromQueryConfig);
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
                _saveConfig,
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
