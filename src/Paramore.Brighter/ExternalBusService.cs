using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Polly;
using Polly.Registry;

namespace Paramore.Brighter
{
    /// <summary>
    /// Provide services to CommandProcessor that persist across the lifetime of the application. Allows separation from elements that have a lifetime linked
    /// to the scope of a request, or are transient for DI purposes
    /// </summary>
    public class ExternalBusService<TMessage, TTransaction> : IAmAnExternalBusService where TMessage : Message
    {
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<CommandProcessor>();

        private readonly IPolicyRegistry<string> _policyRegistry;
        private readonly IAmAnArchiveProvider _archiveProvider;
        private readonly TransformPipelineBuilder _transformPipelineBuilder;
        private readonly TransformPipelineBuilderAsync _transformPipelineBuilderAsync;
        private readonly IAmAnOutboxSync<TMessage, TTransaction> _outBox;
        private readonly IAmAnOutboxAsync<TMessage, TTransaction> _asyncOutbox;
        private readonly int _outboxTimeout;
        private readonly int _outboxBulkChunkSize;
        private readonly IAmAProducerRegistry _producerRegistry;
        private readonly int _archiveBatchSize;
         
        
        private static readonly SemaphoreSlim s_clearSemaphoreToken = new SemaphoreSlim(1, 1);
        private static readonly SemaphoreSlim s_backgroundClearSemaphoreToken = new SemaphoreSlim(1, 1);
        //Used to checking the limit on outstanding messages for an Outbox. We throw at that point. Writes to the static bool should be made thread-safe by locking the object
        private static readonly SemaphoreSlim s_checkOutstandingSemaphoreToken = new SemaphoreSlim(1, 1);

        private const string ADDMESSAGETOOUTBOX = "Add message to outbox";
        private const string ARCHIVE_OUTBOX = "Archive Outbox";
        private const string BULKDISPATCHMESSAGE = "Bulk dispatching messages";
        private const string DEPOSITPOST = "Deposit Post";
        private const string DISPATCHMESSAGE = "Dispatching message";
        private const string GETMESSAGESFROMOUTBOX = "Get outstanding messages from the outbox";

        private DateTime _lastOutStandingMessageCheckAt = DateTime.UtcNow;

        //Uses -1 to indicate no outbox and will thus force a throw on a failed publish
        private int _outStandingCount;
        private bool _disposed;
        private readonly int _maxOutStandingMessages;
        private readonly double _maxOutStandingCheckIntervalMilliSeconds;
        private readonly Dictionary<string, object> _outBoxBag;

        /// <summary>
        /// Creates an instance of External Bus Services
        /// </summary>
        /// <param name="producerRegistry">A registry of producers</param>
        /// <param name="policyRegistry">A registry for reliability policies</param>
        /// <param name="mapperRegistry">The mapper registry; it should also implement IAmAMessageMapperRegistryAsync</param>
        /// <param name="messageTransformerFactory">The factory used to create a transformer pipeline for a message mapper</param>
        /// <param name="messageTransformerFactoryAsync">The factory used to create a transformer pipeline for an async message mapper</param>
        /// <param name="outbox">An outbox for transactional messaging, if none is provided, use an InMemoryOutbox</param>
        /// <param name="archiveProvider">When archiving rows from the Outbox, abstracts to where we should send them</param>
        /// <param name="outboxBulkChunkSize">The size of a chunk for bulk work</param>
        /// <param name="outboxTimeout">How long to timeout for with an outbox</param>
        /// <param name="maxOutStandingMessages">How many messages can become outstanding in the Outbox before we throw an OutboxLimitReached exception</param>
        /// <param name="maxOutStandingCheckIntervalMilliSeconds">How long before we check for maxOutStandingMessages</param>
        /// <param name="outBoxBag">An outbox may require additional arguments, such as a topic list to search</param>
        /// <param name="archiveBatchSize">What batch size to use when archiving from the Outbox</param>
        public ExternalBusService(IAmAProducerRegistry producerRegistry,
            IPolicyRegistry<string> policyRegistry,
            IAmAMessageMapperRegistry mapperRegistry,
            IAmAMessageTransformerFactory messageTransformerFactory,
            IAmAMessageTransformerFactoryAsync messageTransformerFactoryAsync,
            IAmAnOutbox outbox = null,
            IAmAnArchiveProvider archiveProvider = null,
            int outboxBulkChunkSize = 100,
            int outboxTimeout = 300,
            int maxOutStandingMessages = -1,
            int maxOutStandingCheckIntervalMilliSeconds = 1000,
            Dictionary<string, object> outBoxBag = null,
            int archiveBatchSize = 100)
        {
            _producerRegistry = producerRegistry ?? throw new ConfigurationException("Missing Producer Registry for External Bus Services");
            _policyRegistry = policyRegistry?? throw new ConfigurationException("Missing Policy Registry for External Bus Services");
            _archiveProvider = archiveProvider;

            if (mapperRegistry is null) 
                throw new ConfigurationException("A Command Processor with an external bus must have a message mapper registry that implements IAmAMessageMapperRegistry");
            if (mapperRegistry is not IAmAMessageMapperRegistryAsync mapperRegistryAsync)
                throw new ConfigurationException("A Command Processor with an external bus must have a message mapper registry that implements IAmAMessageMapperRegistryAsync");
            if (messageTransformerFactory is null || messageTransformerFactoryAsync is null)
                throw new ConfigurationException("A Command Processor with an external bus must have a message transformer factory");
            
            _transformPipelineBuilder = new TransformPipelineBuilder(mapperRegistry, messageTransformerFactory);
            _transformPipelineBuilderAsync = new TransformPipelineBuilderAsync(mapperRegistryAsync, messageTransformerFactoryAsync);

            //default to in-memory; expectation for a in memory box is Message and CommittableTransaction
            if (outbox is null) outbox = new InMemoryOutbox(TimeProvider.System);
            if (outbox is IAmAnOutboxSync<TMessage, TTransaction> syncOutbox) _outBox = syncOutbox;
            if (outbox is IAmAnOutboxAsync<TMessage, TTransaction> asyncOutbox) _asyncOutbox = asyncOutbox;
            
            _outboxBulkChunkSize = outboxBulkChunkSize;
            _outboxTimeout = outboxTimeout;
            _maxOutStandingMessages = maxOutStandingMessages;
            _maxOutStandingCheckIntervalMilliSeconds = maxOutStandingCheckIntervalMilliSeconds;
            _outBoxBag = outBoxBag;
            _archiveBatchSize = archiveBatchSize;

            ConfigureCallbacks();
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing && _producerRegistry != null)
                _producerRegistry.CloseAll();
            _disposed = true;
        }

        /// <summary>
        /// Adds a message to the outbox
        /// </summary>
        /// <param name="request">The request that we are storing (used for id)</param>
        /// <param name="message">The message to store in the outbox</param>
        /// <param name="overridingTransactionProvider">The provider of the transaction for the outbox</param>
        /// <param name="continueOnCapturedContext">Use the same thread for a callback</param>
        /// <param name="cancellationToken">Allow cancellation of the message</param>
        /// <typeparam name="TRequest">The type of request we are saving</typeparam>
        /// <exception cref="ChannelFailureException">Thrown if we cannot write to the outbox</exception>
        public async Task AddToOutboxAsync<TRequest>(
            TRequest request,
            TMessage message,
            IAmABoxTransactionProvider<TTransaction> overridingTransactionProvider = null,
            bool continueOnCapturedContext = false,
            CancellationToken cancellationToken = default) where TRequest : IRequest
        {
            CheckOutboxOutstandingLimit();

            var written = await RetryAsync(
                async ct =>
                {
                    await _asyncOutbox.AddAsync(message, _outboxTimeout, overridingTransactionProvider, ct)
                        .ConfigureAwait(continueOnCapturedContext);
                },
                continueOnCapturedContext, cancellationToken).ConfigureAwait(continueOnCapturedContext);

            if (!written)
                throw new ChannelFailureException($"Could not write request {request.Id} to the outbox");
            Activity.Current?.AddEvent(new ActivityEvent(ADDMESSAGETOOUTBOX,
                tags: new ActivityTagsCollection { { "MessageId", message.Id } }));
        }

        /// <summary>
        /// Adds a message to the outbox
        /// </summary>
        /// <param name="messages">The messages to store in the outbox</param>
        /// <param name="overridingTransactionProvider"></param>
        /// <param name="continueOnCapturedContext">Use the same thread for a callback</param>
        /// <param name="cancellationToken">Allow cancellation of the message</param>
        /// <param name="overridingTransactionProvider ">The provider of the transaction for the outbox</param>
        /// <exception cref="ChannelFailureException">Thrown if we cannot write to the outbox</exception>
        public async Task AddToOutboxAsync(
            IEnumerable<TMessage> messages,
            IAmABoxTransactionProvider<TTransaction> overridingTransactionProvider = null,
            bool continueOnCapturedContext = false,
            CancellationToken cancellationToken = default)
        {
            CheckOutboxOutstandingLimit();

            foreach (var chunk in ChunkMessages(messages))
            {
                var written = await RetryAsync(
                    async ct =>
                    {
                        await _asyncOutbox.AddAsync(chunk, _outboxTimeout, overridingTransactionProvider, ct).ConfigureAwait(continueOnCapturedContext);
                    },
                    continueOnCapturedContext, cancellationToken).ConfigureAwait(continueOnCapturedContext);

                if (!written)
                    throw new ChannelFailureException($"Could not write {chunk.Count()} requests to the outbox");
            }
        }

        /// <summary>
        /// Adds a message to the outbox
        /// </summary>
        /// <param name="request">The request the message is composed from (used for diagnostics)</param>
        /// <param name="message">The message we intend to send</param>
        /// <param name="overridingTransactionProvider">A transaction provider that gives us the transaction to use with the Outbox</param>
        /// <typeparam name="TTransaction">The transaction type for the Outbox</typeparam>
        /// <typeparam name="TRequest">The type of the request we have converted into a message</typeparam>
        /// <exception cref="ChannelFailureException"></exception>
        public void AddToOutbox<TRequest>(
            TRequest request,
            TMessage message,
            IAmABoxTransactionProvider<TTransaction> overridingTransactionProvider = null)
            where TRequest : class, IRequest
        {
            CheckOutboxOutstandingLimit();

            var written = Retry(() => { _outBox.Add(message, _outboxTimeout, overridingTransactionProvider); });

            if (!written)
                throw new ChannelFailureException($"Could not write request {request.Id} to the outbox");
            Activity.Current?.AddEvent(new ActivityEvent(ADDMESSAGETOOUTBOX,
                tags: new ActivityTagsCollection { { "MessageId", message.Id } }));
        }

        public void AddToOutbox(
            IEnumerable<TMessage> messages,
            IAmABoxTransactionProvider<TTransaction> overridingTransactionProvider = null
            )
        {
            CheckOutboxOutstandingLimit();

            foreach (var chunk in ChunkMessages(messages))
            {
                var written =
                    Retry(() => { _outBox.Add(chunk, _outboxTimeout, overridingTransactionProvider); });

                if (!written)
                    throw new ChannelFailureException($"Could not write {chunk.Count()} messages to the outbox");
            }
        }
        
        /// <summary>
        /// Archive Message from the outbox to the outbox archive provider
        /// Throws any archiving exception
        /// </summary>
        /// <param name="millisecondsDispatchedSince">Minimum age in hours</param>
        public void Archive(int millisecondsDispatchedSince)
        {
            try
            {
                var messages = _outBox.DispatchedMessages(millisecondsDispatchedSince, _archiveBatchSize);
                
                s_logger.LogInformation(
                    "Found {NumberOfMessageArchived} message to archive to {MessagesToArchive}, batch size : {BatchSize}", 
                    messages.Count(), _archiveBatchSize
                );
 
                if (!messages.Any()) return;
                
                foreach (var message in messages)
                {
                    _archiveProvider.ArchiveMessage(message);
                }

                _outBox.Delete(messages.Select(e => e.Id).ToArray());
                
                s_logger.LogInformation(
                    "Successfully archived {NumberOfMessageArchived}, batch size : {BatchSize}", 
                    messages.Count(),
                    _archiveBatchSize
                );
            }
            catch (Exception e)
            {
                s_logger.LogError(e, "Error while archiving from the outbox");
                throw;
            }
        }

        /// <summary>
        /// Archive Message from the outbox to the outbox archive provider
        /// Throws any archiving exception
        /// </summary>
        /// <param name="minimumAge">Minimum age in hours</param>
        /// <param name="cancellationToken">The Cancellation Token</param>
        public async Task ArchiveAsync(int minimumAge, CancellationToken cancellationToken)
        {
            try
            {
                var messages = await _asyncOutbox.DispatchedMessagesAsync(
                    minimumAge, _archiveBatchSize, cancellationToken:cancellationToken);

                if (!messages.Any()) return;

                await _asyncOutbox.DeleteAsync(
                    messages.Select(e => e.Id).ToArray(), cancellationToken: cancellationToken);
            }
            catch (Exception e)
            {
                s_logger.LogError(e, "Error while archiving from the outbox");
                throw;
            }
        }
        
        /// <summary>
        /// Used with RPC to call a remote service via the external bus
        /// </summary>
        /// <param name="outMessage">The message to send</param>
        /// <typeparam name="T">The type of the call</typeparam>
        /// <typeparam name="TResponse">The type of the response</typeparam>
        public void CallViaExternalBus<T, TResponse>(Message outMessage)
            where T : class, ICall where TResponse : class, IResponse
        {
            //We assume that this only occurs over a blocking producer
            var producer = _producerRegistry.LookupBy(outMessage.Header.Topic);
            if (producer is IAmAMessageProducerSync producerSync)
                Retry(() => producerSync.Send(outMessage));
        }

        /// <summary>
        /// This is the clear outbox for explicit clearing of messages.
        /// </summary>
        /// <param name="posts">The ids of the posts that you would like to clear</param>
        /// <param name="args"></param>
        /// <exception cref="InvalidOperationException">Thrown if there is no async outbox defined</exception>
        /// <exception cref="NullReferenceException">Thrown if a message cannot be found</exception>
        public void ClearOutbox(string[] posts, Dictionary<string, object> args = null)
        {
            if (!HasOutbox())
                throw new InvalidOperationException("No outbox defined.");

            // Only allow a single Clear to happen at a time
            s_clearSemaphoreToken.Wait();
            try
            {
                foreach (var messageId in posts)
                {
                    var message = _outBox.Get(messageId);
                    if (message == null || message.Header.MessageType == MessageType.MT_NONE)
                        throw new NullReferenceException($"Message with Id {messageId} not found in the Outbox");

                    Dispatch(new[] { message }, args);
                }
            }
            finally
            {
                s_clearSemaphoreToken.Release();
            }

            CheckOutstandingMessages();
        }

        /// <summary>
        /// This is the clear outbox for explicit clearing of messages.
        /// </summary>
        /// <param name="posts">The ids of the posts that you would like to clear</param>
        /// <param name="continueOnCapturedContext">Should we use the same thread in the callback</param>
        /// <param name="args">For outboxes that require additional parameters such as topic, provide an optional arg</param>
        /// <param name="cancellationToken">Allow cancellation of the operation</param>
        /// <exception cref="InvalidOperationException">Thrown if there is no async outbox defined</exception>
        /// <exception cref="NullReferenceException">Thrown if a message cannot be found</exception>
        public async Task ClearOutboxAsync(IEnumerable<string> posts,
            bool continueOnCapturedContext = false,
            Dictionary<string, object> args = null,
            CancellationToken cancellationToken = default)
        {
            if (!HasAsyncOutbox())
                throw new InvalidOperationException("No async outbox defined.");

            await s_clearSemaphoreToken.WaitAsync(cancellationToken);
            try
            {
                foreach (var messageId in posts)
                {
                    var message = await _asyncOutbox.GetAsync(messageId, _outboxTimeout, args, cancellationToken);
                    if (message == null || message.Header.MessageType == MessageType.MT_NONE)
                        throw new NullReferenceException($"Message with Id {messageId} not found in the Outbox");

                    await DispatchAsync(new[] { message }, continueOnCapturedContext, cancellationToken);
                }
            }
            finally
            {
                s_clearSemaphoreToken.Release();
            }

            CheckOutstandingMessages();
        }

        /// <summary>
        /// This is the clear outbox for explicit clearing of messages.
        /// </summary>
        /// <param name="amountToClear">Maximum number to clear.</param>
        /// <param name="minimumAge">The minimum age of messages to be cleared in milliseconds.</param>
        /// <param name="useAsync">Use the Async outbox and Producer</param>
        /// <param name="useBulk">Use bulk sending capability of the message producer, this must be paired with useAsync.</param>
        /// <param name="args">Optional bag of arguments required by an outbox implementation to sweep</param>
        public void ClearOutbox(
            int amountToClear,
            int minimumAge,
            bool useAsync,
            bool useBulk,
            Dictionary<string, object> args = null)
        {
            var span = Activity.Current;
            span?.AddTag("amountToClear", amountToClear);
            span?.AddTag("millisecondsSinceSent", minimumAge);
            span?.AddTag("async", useAsync);
            span?.AddTag("bulk", useBulk);

            if (useAsync)
            {
                if (!HasAsyncOutbox())
                    throw new InvalidOperationException("No async outbox defined.");

                Task.Run(() => BackgroundDispatchUsingAsync(amountToClear, minimumAge, useBulk, args),
                    CancellationToken.None);
            }

            else
            {
                if (!HasOutbox())
                    throw new InvalidOperationException("No outbox defined.");

                Task.Run(() => BackgroundDispatchUsingSync(amountToClear, minimumAge, args));
            }
        }
        
        /// <summary>
        /// Given a request, run the transformation pipeline to create a message
        /// </summary>
        /// <param name="request">The request</param>
        /// <typeparam name="TRequest">the type of the request</typeparam>
        /// <typeparam name="TTransaction"></typeparam>
        /// <returns></returns>
        public Message CreateMessageFromRequest<TRequest>(TRequest request) where TRequest : class, IRequest
        {
            var message = MapMessage(request);
            AddTelemetryToMessage<TRequest>(message);
            return message;
        }

        /// <summary>
        /// Given a request, run the transformation pipeline to create a message 
        /// </summary>
        /// <param name="request">The request</param>
        /// <param name="cancellationToken">Cancel the in-flight operation</param>
        /// <typeparam name="TRequest">The type of the request</typeparam>
        /// <typeparam name="TTransaction"></typeparam>
        /// <returns></returns>
        public async Task<Message> CreateMessageFromRequestAsync<TRequest>(TRequest request,
            CancellationToken cancellationToken) where TRequest : class, IRequest
        {
            Message message = await MapMessageAsync(request, cancellationToken);
            AddTelemetryToMessage<TRequest>(message);
            return message;
        }
        
        /// <summary>
        /// Given a set of messages, map them to requests
        /// </summary>
        /// <param name="requestType">The type of the request</param>
        /// <param name="requests">The list of requests</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task<List<Message>> CreateMessagesFromRequests(
            Type requestType, 
            IEnumerable<IRequest> requests,
            CancellationToken cancellationToken)
        {
            var parameters = new object[] { requests, cancellationToken };

            var hasAsyncPipeline = (bool) typeof(TransformPipelineBuilderAsync)
                .GetMethod(nameof(TransformPipelineBuilderAsync.HasPipeline),
                    BindingFlags.Instance | BindingFlags.Public)
                .MakeGenericMethod(requestType)
                .Invoke(this._transformPipelineBuilderAsync, null);
            
            if (hasAsyncPipeline)
            {
                return (Task<List<Message>>) GetType()
                    .GetMethod(nameof(BulkMapMessagesAsync), BindingFlags.Instance | BindingFlags.NonPublic)
                    .MakeGenericMethod(requestType)
                    .Invoke(this, parameters); 
            }
            
            var tcs = new TaskCompletionSource<List<Message>>();
            tcs.SetResult((List<Message>)GetType()
                .GetMethod(nameof(BulkMapMessages), BindingFlags.Instance | BindingFlags.NonPublic)
                .MakeGenericMethod(requestType)                                                             
                .Invoke(this, new[] { requests }));
            return tcs.Task;
        }

        /// <summary>
        /// Intended for usage with the CommandProcessor's Call method, this method will create a request from a message
        /// </summary>
        /// <param name="message">The message that forms a reply to a call</param>
        /// <param name="request">The request constructed from that message</param>
        /// <typeparam name="TRequest">The type of the request</typeparam>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if there is no message mapper for the request</exception>
        public void CreateRequestFromMessage<TRequest>(Message message, out TRequest request)
            where TRequest : class, IRequest
        {
            if (_transformPipelineBuilderAsync.HasPipeline<TRequest>())
            {
                request = _transformPipelineBuilderAsync
                    .BuildUnwrapPipeline<TRequest>()
                    .UnwrapAsync(message)
                    .GetAwaiter()
                    .GetResult();
            }
            else if (_transformPipelineBuilder.HasPipeline<TRequest>())
            {
                request = _transformPipelineBuilder
                    .BuildUnwrapPipeline<TRequest>()
                    .Unwrap(message);

            } 
            else
            {
                throw new ArgumentOutOfRangeException("No message mapper defined for request");
            }
        } 
        
        /// <summary>
        /// Do we have an async outbox defined?
        /// </summary>
        /// <returns>true if defined</returns>
        public bool HasAsyncOutbox()
        {
            return _asyncOutbox != null;
        }

        /// <summary>
        /// Do we have a synchronous outbox defined?
        /// </summary>
        /// <returns>true if defined</returns>
        public bool HasOutbox()
        {
            return _outBox != null;
        }

 
        private void AddTelemetryToMessage<T>(Message message)
        {
            var activity = Activity.Current ??
                           ApplicationTelemetry.ActivitySource.StartActivity(DEPOSITPOST, ActivityKind.Producer);

            if (activity != null)
            {
                message.Header.AddTelemetryInformation(activity, typeof(T).ToString());
            }
        }
        
  
        private async Task BackgroundDispatchUsingSync(
            int amountToClear, 
            int millisecondsSinceSent,
            Dictionary<string, object> args
            )
        {
            var span = Activity.Current;
            if (await s_backgroundClearSemaphoreToken.WaitAsync(TimeSpan.Zero))
            {
                await s_clearSemaphoreToken.WaitAsync(CancellationToken.None);
                try
                {
                    var messages = _outBox.OutstandingMessages(millisecondsSinceSent, amountToClear, args: args);
                    span?.AddEvent(new ActivityEvent(GETMESSAGESFROMOUTBOX,
                        tags: new ActivityTagsCollection { { "Outstanding Messages", messages.Count() } }));
                    s_logger.LogInformation("Found {NumberOfMessages} to clear out of amount {AmountToClear}",
                        messages.Count(), amountToClear);
                    Dispatch(messages, args);
                    s_logger.LogInformation("Messages have been cleared");
                    span?.SetStatus(ActivityStatusCode.Ok);
                }
                catch (Exception e)
                {
                    span?.SetStatus(ActivityStatusCode.Error, "Error while dispatching from outbox");
                    s_logger.LogError(e, "Error while dispatching from outbox");
                }
                finally
                {
                    span?.Dispose();
                    s_clearSemaphoreToken.Release();
                    s_backgroundClearSemaphoreToken.Release();
                }

                CheckOutstandingMessages();
            }
            else
            {
                span?.SetStatus(ActivityStatusCode.Error)
                    .Dispose();
                s_logger.LogInformation("Skipping dispatch of messages as another thread is running");
            }
        }
        
        private async Task BackgroundDispatchUsingAsync(
            int amountToClear, 
            int milliSecondsSinceSent, 
            bool useBulk,
            Dictionary<string, object> args)
        {
            var span = Activity.Current;
            if (await s_backgroundClearSemaphoreToken.WaitAsync(TimeSpan.Zero))
            {
                await s_clearSemaphoreToken.WaitAsync(CancellationToken.None);
                try
                {
                    var messages =
                        await _asyncOutbox.OutstandingMessagesAsync(milliSecondsSinceSent, amountToClear, args: args);
                    span?.AddEvent(new ActivityEvent(GETMESSAGESFROMOUTBOX));

                    s_logger.LogInformation("Found {NumberOfMessages} to clear out of amount {AmountToClear}",
                        messages.Count(), amountToClear);

                    if (useBulk)
                    {
                        await BulkDispatchAsync(messages, CancellationToken.None);
                    }
                    else
                    {
                        await DispatchAsync(messages, false, CancellationToken.None);
                    }

                    span?.SetStatus(ActivityStatusCode.Ok);
                    s_logger.LogInformation("Messages have been cleared");
                }
                catch (Exception e)
                {
                    s_logger.LogError(e, "Error while dispatching from outbox");
                    span?.SetStatus(ActivityStatusCode.Error, "Error while dispatching from outbox");
                }
                finally
                {
                    span?.Dispose();
                    s_clearSemaphoreToken.Release();
                    s_backgroundClearSemaphoreToken.Release();
                }

                CheckOutstandingMessages();
            }
            else
            {
                span?.SetStatus(ActivityStatusCode.Error)
                    .Dispose();
                s_logger.LogInformation("Skipping dispatch of messages as another thread is running");
            }
        }
        
        private async Task BulkDispatchAsync(IEnumerable<Message> posts, CancellationToken cancellationToken)
        {
            var span = Activity.Current;
            //Chunk into Topics
            var messagesByTopic = posts.GroupBy(m => m.Header.Topic);

            foreach (var topicBatch in messagesByTopic)
            {
                var producer = _producerRegistry.LookupBy(topicBatch.Key);

                if (producer is IAmABulkMessageProducerAsync bulkMessageProducer)
                {
                    var messages = topicBatch.ToArray();
                    s_logger.LogInformation("Bulk Dispatching {NumberOfMessages} for Topic {TopicName}",
                        messages.Length, topicBatch.Key);
                    span?.AddEvent(new ActivityEvent(BULKDISPATCHMESSAGE,
                        tags: new ActivityTagsCollection
                        {
                            { "Topic", topicBatch.Key }, { "Number Of Messages", messages.Length }
                        }));
                    var dispatchesMessages = bulkMessageProducer.SendAsync(messages, cancellationToken);

                    await foreach (var successfulMessage in dispatchesMessages)
                    {
                        if (!(producer is ISupportPublishConfirmation))
                        {
                            await RetryAsync(async ct => await _asyncOutbox.MarkDispatchedAsync(
                                    successfulMessage, DateTime.UtcNow, cancellationToken: cancellationToken),
                                cancellationToken: cancellationToken);
                        }
                    }
                }
                else
                {
                    throw new InvalidOperationException("No async bulk message producer defined.");
                }
            }
        }
        
        private List<Message> BulkMapMessages<T>(IEnumerable<IRequest> requests) where T : class, IRequest
        {
            return requests.Select(r =>
            {
                var publication = _producerRegistry.LookupPublication<T>();
                var wrapPipeline = _transformPipelineBuilder.BuildWrapPipeline<T>();
                var message = wrapPipeline.Wrap((T)r, publication);
                AddTelemetryToMessage<T>(message);
                return message;
            }).ToList();
        }

        private async Task<List<Message>> BulkMapMessagesAsync<T>(
            IEnumerable<IRequest> requests,
            CancellationToken cancellationToken = default
        ) where T : class, IRequest
        {
            var messages = new List<Message>();
            foreach (var request in requests)
            {
                var publication = _producerRegistry.LookupPublication<T>();
                var wrapPipeline = _transformPipelineBuilderAsync.BuildWrapPipeline<T>();
                var message = await wrapPipeline.WrapAsync((T)request,publication, cancellationToken);
                AddTelemetryToMessage<T>(message);
                messages.Add(message);
            }

            return messages;
        }
        
        private IEnumerable<IEnumerable<TMessage>> ChunkMessages(IEnumerable<TMessage> messages)
        {
            return Enumerable.Range(0, (int)Math.Ceiling((messages.Count() / (decimal)_outboxBulkChunkSize)))
                .Select(i => new List<TMessage>(messages
                    .Skip(i * _outboxBulkChunkSize)
                    .Take(_outboxBulkChunkSize)
                    .ToArray()));
        }

        private void CheckOutboxOutstandingLimit()
        {
            bool hasOutBox = (_outBox != null || _asyncOutbox != null);
            if (!hasOutBox)
                return;
            
            s_logger.LogDebug("Outbox outstanding message count is: {OutstandingMessageCount}", _outStandingCount);
            // Because a thread recalculates this, we may always be in a delay, so we check on entry for the next outstanding item
            bool exceedsOutstandingMessageLimit = _maxOutStandingMessages != -1 && _outStandingCount > _maxOutStandingMessages;

            if (exceedsOutstandingMessageLimit)
                throw new OutboxLimitReachedException(
                    $"The outbox limit of {_maxOutStandingMessages} has been exceeded");
        }

        private void CheckOutstandingMessages()
        {
            var now = DateTime.UtcNow;
            var checkInterval =
                TimeSpan.FromMilliseconds(_maxOutStandingCheckIntervalMilliSeconds);


            var timeSinceLastCheck = now - _lastOutStandingMessageCheckAt;
            s_logger.LogDebug("Time since last check is {SecondsSinceLastCheck} seconds.",
                timeSinceLastCheck.TotalSeconds);
            if (timeSinceLastCheck < checkInterval)
            {
                s_logger.LogDebug($"Check not ready to run yet");
                return;
            }

            s_logger.LogDebug(
                "Running outstanding message check at {MessageCheckTime} after {SecondsSinceLastCheck} seconds wait",
                DateTime.UtcNow, timeSinceLastCheck.TotalSeconds);
            //This is expensive, so use a background thread
            Task.Run(() => OutstandingMessagesCheck());
        }
        
        private void ConfigureCallbacks()
        {
            //Only register one, to avoid two callbacks where we support both interfaces on a producer
            foreach (var producer in _producerRegistry.Producers)
            {
                if (!ConfigurePublisherCallbackMaybe(producer))
                    ConfigureAsyncPublisherCallbackMaybe(producer);
            }
        }

        private void ConfigureAsyncPublisherCallbackMaybe(IAmAMessageProducer producer)
        {
            if (producer is ISupportPublishConfirmation producerSync)
            {
                producerSync.OnMessagePublished += async delegate(bool success, string id)
                {
                    if (success)
                    {
                        s_logger.LogInformation("Sent message: Id:{Id}", id);
                        if (_asyncOutbox != null)
                            await RetryAsync(async ct =>
                                await _asyncOutbox.MarkDispatchedAsync(id, DateTime.UtcNow, cancellationToken: ct));
                    }
                };
            }
        }

        private bool ConfigurePublisherCallbackMaybe(IAmAMessageProducer producer)
        {
            if (producer is ISupportPublishConfirmation producerSync)
            {
                producerSync.OnMessagePublished += delegate(bool success, string id)
                {
                    if (success)
                    {
                        s_logger.LogInformation("Sent message: Id:{Id}", id);
                        if (_outBox != null)
                            Retry(() => _outBox.MarkDispatched(id, DateTime.UtcNow));
                    }
                };
                return true;
            }

            return false;
        }
 
        private void Dispatch(IEnumerable<Message> posts, Dictionary<string, object> args = null)
        {
            foreach (var message in posts)
            {
                Activity.Current?.AddEvent(new ActivityEvent(DISPATCHMESSAGE,
                    tags: new ActivityTagsCollection
                    {
                        { "Topic", message.Header.Topic }, { "MessageId", message.Id }
                    }));
                s_logger.LogInformation("Decoupled invocation of message: Topic:{Topic} Id:{Id}", message.Header.Topic,
                    message.Id.ToString());

                var producer = _producerRegistry.LookupBy(message.Header.Topic);

                if (producer is IAmAMessageProducerSync producerSync)
                {
                    if (producer is ISupportPublishConfirmation)
                    {
                        //mark dispatch handled by a callback - set in constructor
                        Retry(() => { producerSync.Send(message); });
                    }
                    else
                    {
                        var sent = Retry(() => { producerSync.Send(message); });
                        if (sent)
                            Retry(() => _outBox.MarkDispatched(message.Id, DateTime.UtcNow, args));
                    }
                }
                else
                    throw new InvalidOperationException("No sync message producer defined.");
            }
        }

        private async Task DispatchAsync(IEnumerable<Message> posts, bool continueOnCapturedContext,
            CancellationToken cancellationToken)
        {
            foreach (var message in posts)
            {
                Activity.Current?.AddEvent(new ActivityEvent(DISPATCHMESSAGE,
                    tags: new ActivityTagsCollection
                    {
                        { "Topic", message.Header.Topic }, { "MessageId", message.Id }
                    }));
                s_logger.LogInformation("Decoupled invocation of message: Topic:{Topic} Id:{Id}", message.Header.Topic,
                    message.Id.ToString());

                var producer = _producerRegistry.LookupBy(message.Header.Topic);

                if (producer is IAmAMessageProducerAsync producerAsync)
                {
                    if (producer is ISupportPublishConfirmation)
                    {
                        //mark dispatch handled by a callback - set in constructor
                        await RetryAsync(
                                async ct =>
                                    await producerAsync.SendAsync(message).ConfigureAwait(continueOnCapturedContext),
                                continueOnCapturedContext,
                                cancellationToken)
                            .ConfigureAwait(continueOnCapturedContext);
                    }
                    else
                    {
                        var sent = await RetryAsync(
                                async ct =>
                                    await producerAsync.SendAsync(message).ConfigureAwait(continueOnCapturedContext),
                                continueOnCapturedContext,
                                cancellationToken)
                            .ConfigureAwait(continueOnCapturedContext);

                        if (sent)
                            await RetryAsync(
                                async ct => await _asyncOutbox.MarkDispatchedAsync(message.Id, DateTime.UtcNow,
                                    cancellationToken: cancellationToken),
                                cancellationToken: cancellationToken);
                    }
                }
                else
                    throw new InvalidOperationException("No async message producer defined.");
            }
        }
        
        private Message MapMessage<TRequest>(TRequest request)
            where TRequest : class, IRequest
        {
            var publication = _producerRegistry.LookupPublication<TRequest>();
            if (publication == null)
                throw new ConfigurationException(
                    $"No publication found for request {request.GetType().Name}");
            
            Message message;
            if (_transformPipelineBuilder.HasPipeline<TRequest>())
            {
                message = _transformPipelineBuilder
                    .BuildWrapPipeline<TRequest>()
                    .Wrap(request, publication);
            }                                                
            else
            {
                throw new ArgumentOutOfRangeException("No message mapper defined for request");
            }

            return message;
        }

        private async Task<Message> MapMessageAsync<TRequest>(TRequest request, CancellationToken cancellationToken)
            where TRequest : class, IRequest
        {
            var publication = _producerRegistry.LookupPublication<TRequest>();
            if (publication == null)
                throw new ConfigurationException(
                    $"No publication found for request {request.GetType().Name}");
            
            Message message;
            if (_transformPipelineBuilderAsync.HasPipeline<TRequest>())
            {
                message = await _transformPipelineBuilderAsync
                    .BuildWrapPipeline<TRequest>()
                    .WrapAsync(request, publication, cancellationToken);
            }
            else
            {
                throw new ArgumentOutOfRangeException("No message mapper defined for request");
            }

            return message;
        }

        private void OutstandingMessagesCheck()
        {
            s_checkOutstandingSemaphoreToken.Wait();

            _lastOutStandingMessageCheckAt = DateTime.UtcNow;
            s_logger.LogDebug("Begin count of outstanding messages");
            try
            {
                if (_outBox != null)
                {
                    _outStandingCount = _outBox
                        .OutstandingMessages(
                            _maxOutStandingCheckIntervalMilliSeconds,
                            args: _outBoxBag 
                        )
                        .Count();
                    return;
                }

                _outStandingCount = 0;
            }
            catch (Exception ex)
            {
                //if we can't talk to the outbox, we would swallow the exception on this thread
                //by setting the _outstandingCount to -1, we force an exception
                s_logger.LogError(ex, "Error getting outstanding message count, reset count");
                _outStandingCount = 0;
            }
            finally
            {
                s_logger.LogDebug("Current outstanding count is {OutStandingCount}", _outStandingCount);
                s_checkOutstandingSemaphoreToken.Release();
            }
        }
        
        public bool Retry(Action action)
        {
            var policy = _policyRegistry.Get<Policy>(CommandProcessor.RETRYPOLICY);
            var result = policy.ExecuteAndCapture(action);
            if (result.Outcome != OutcomeType.Successful)
            {
                if (result.FinalException != null)
                {
                    s_logger.LogError(result.FinalException, "Exception whilst trying to publish message");
                    CheckOutstandingMessages();
                }

                return false;
            }

            return true;
        }

        private async Task<bool> RetryAsync(Func<CancellationToken, Task> send, bool continueOnCapturedContext = false,
            CancellationToken cancellationToken = default)
        {
            var result = await _policyRegistry.Get<AsyncPolicy>(CommandProcessor.RETRYPOLICYASYNC)
                .ExecuteAndCaptureAsync(send, cancellationToken, continueOnCapturedContext)
                .ConfigureAwait(continueOnCapturedContext);

            if (result.Outcome != OutcomeType.Successful)
            {
                if (result.FinalException != null)
                {
                    s_logger.LogError(result.FinalException, "Exception whilst trying to publish message");
                    CheckOutstandingMessages();
                }

                return false;
            }

            return true;
        }
    }
}
