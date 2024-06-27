using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Observability;
using Polly;
using Polly.Registry;

namespace Paramore.Brighter
{
    /// <summary>
    /// Provide services to CommandProcessor that persist across the lifetime of the application. Allows separation from
    /// elements that have a lifetime linked to the scope of a request, or are transient for DI purposes
    /// </summary>
    public class ExternalBusService<TMessage, TTransaction> : IAmAnExternalBusService, IAmAnExternalBusService<TMessage, TTransaction>
        where TMessage : Message
    {
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<CommandProcessor>();

        private readonly IPolicyRegistry<string> _policyRegistry;
        private readonly IAmAnArchiveProvider _archiveProvider;
        private readonly TransformPipelineBuilder _transformPipelineBuilder;
        private readonly TransformPipelineBuilderAsync _transformPipelineBuilderAsync;
        private readonly IAmAnOutboxSync<TMessage, TTransaction> _outBox;
        private readonly IAmAnOutboxAsync<TMessage, TTransaction> _asyncOutbox;
        private readonly int _outboxTimeout;
        private readonly IAmAProducerRegistry _producerRegistry;
        private readonly int _archiveBatchSize;
        private readonly InstrumentationOptions _instrumentationOptions;

        private static readonly SemaphoreSlim s_clearSemaphoreToken = new SemaphoreSlim(1, 1);

        private static readonly SemaphoreSlim s_backgroundClearSemaphoreToken = new SemaphoreSlim(1, 1);

        //Used to checking the limit on outstanding messages for an Outbox. We throw at that point. Writes to the static
        //bool should be made thread-safe by locking the object
        private static readonly SemaphoreSlim s_checkOutstandingSemaphoreToken = new SemaphoreSlim(1, 1);

        private const string BULKDISPATCHMESSAGE = "Bulk dispatching messages";

        private DateTime _lastOutStandingMessageCheckAt = DateTime.UtcNow;

        //Uses -1 to indicate no outbox and will thus force a throw on a failed publish
        private int _outStandingCount;
        private bool _disposed;
        private readonly int _maxOutStandingMessages;
        private readonly double _maxOutStandingCheckIntervalMilliSeconds;
        private readonly Dictionary<string, object> _outBoxBag;
        private readonly IAmABrighterTracer _tracer;

        /// <summary>
        /// Creates an instance of External Bus Services
        /// </summary>
        /// <param name="producerRegistry">A registry of producers</param>
        /// <param name="policyRegistry">A registry for reliability policies</param>
        /// <param name="mapperRegistry">The mapper registry; it should also implement IAmAMessageMapperRegistryAsync</param>
        /// <param name="messageTransformerFactory">The factory used to create a transformer pipeline for a message mapper</param>
        /// <param name="messageTransformerFactoryAsync">The factory used to create a transformer pipeline for an async message mapper</param>
        /// <param name="tracer"></param>
        /// <param name="outbox">An outbox for transactional messaging, if none is provided, use an InMemoryOutbox</param>
        /// <param name="archiveProvider">When archiving rows from the Outbox, abstracts to where we should send them</param>
        /// <param name="requestContextFactory"></param>
        /// <param name="outboxTimeout">How long to timeout for with an outbox</param>
        /// <param name="maxOutStandingMessages">How many messages can become outstanding in the Outbox before we throw an OutboxLimitReached exception</param>
        /// <param name="maxOutStandingCheckIntervalMilliSeconds">How long before we check for maxOutStandingMessages</param>
        /// <param name="outBoxBag">An outbox may require additional arguments, such as a topic list to search</param>
        /// <param name="archiveBatchSize">What batch size to use when archiving from the Outbox</param>
        /// <param name="instrumentationOptions">How verbose do we want our instrumentation to be</param>
        public ExternalBusService(
            IAmAProducerRegistry producerRegistry,
            IPolicyRegistry<string> policyRegistry,
            IAmAMessageMapperRegistry mapperRegistry,
            IAmAMessageTransformerFactory messageTransformerFactory,
            IAmAMessageTransformerFactoryAsync messageTransformerFactoryAsync,
            IAmABrighterTracer tracer,
            IAmAnOutbox outbox = null,
            IAmAnArchiveProvider archiveProvider = null,
            IAmARequestContextFactory requestContextFactory = null,
            int outboxTimeout = 300,
            int maxOutStandingMessages = -1,
            int maxOutStandingCheckIntervalMilliSeconds = 1000,
            Dictionary<string, object> outBoxBag = null,
            int archiveBatchSize = 100,
            InstrumentationOptions instrumentationOptions = InstrumentationOptions.All)
        {
            _producerRegistry = producerRegistry ?? throw new ConfigurationException("Missing Producer Registry for External Bus Services");
            _policyRegistry = policyRegistry?? throw new ConfigurationException("Missing Policy Registry for External Bus Services");
            _archiveProvider = archiveProvider;
            
            requestContextFactory ??= new InMemoryRequestContextFactory();
            
            if (mapperRegistry is null) 
                throw new ConfigurationException("A Command Processor with an external bus must have a message mapper registry that implements IAmAMessageMapperRegistry");
            if (mapperRegistry is not IAmAMessageMapperRegistryAsync mapperRegistryAsync)
                throw new ConfigurationException(
                    "A Command Processor with an external bus must have a message mapper registry that implements IAmAMessageMapperRegistryAsync");
            if (messageTransformerFactory is null || messageTransformerFactoryAsync is null)
                throw new ConfigurationException(
                    "A Command Processor with an external bus must have a message transformer factory");

            _transformPipelineBuilder = new TransformPipelineBuilder(mapperRegistry, messageTransformerFactory);
            _transformPipelineBuilderAsync =
                new TransformPipelineBuilderAsync(mapperRegistryAsync, messageTransformerFactoryAsync);

            //default to in-memory; expectation for a in memory box is Message and CommittableTransaction
            outbox ??= new InMemoryOutbox(TimeProvider.System);
            outbox.Tracer = tracer;
            
            if (outbox is IAmAnOutboxSync<TMessage, TTransaction> syncOutbox) _outBox = syncOutbox;
            if (outbox is IAmAnOutboxAsync<TMessage, TTransaction> asyncOutbox) _asyncOutbox = asyncOutbox;

            _outboxTimeout = outboxTimeout;
            _maxOutStandingMessages = maxOutStandingMessages;
            _maxOutStandingCheckIntervalMilliSeconds = maxOutStandingCheckIntervalMilliSeconds;
            _outBoxBag = outBoxBag;
            _archiveBatchSize = archiveBatchSize;
            _instrumentationOptions = instrumentationOptions;
            _tracer = tracer;

            ConfigureCallbacks(requestContextFactory.Create());
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
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
        /// <param name="message">The message to store in the outbox</param>
        /// <param name="requestContext">The context of the request pipeline</param>
        /// <param name="overridingTransactionProvider">The provider of the transaction for the outbox</param>
        /// <param name="continueOnCapturedContext">Use the same thread for a callback</param>
        /// <param name="cancellationToken">Allow cancellation of the message</param>
        /// <typeparam name="TTransaction">The type of the transaction used to add to the Outbox</typeparam>
        /// <exception cref="ChannelFailureException">Thrown if we cannot write to the Outbox</exception>
        public async Task AddToOutboxAsync(
            TMessage message,
            RequestContext requestContext,
            IAmABoxTransactionProvider<TTransaction> overridingTransactionProvider = null,
            bool continueOnCapturedContext = false,
            CancellationToken cancellationToken = default) 
        {
            CheckOutboxOutstandingLimit();
            
            BrighterTracer.WriteOutboxEvent(OutboxDbOperation.Add, message, requestContext.Span, 
                overridingTransactionProvider != null, true, _instrumentationOptions); 

            var written = await RetryAsync(
                async ct =>
                {
                    await _asyncOutbox.AddAsync(message, requestContext, _outboxTimeout, overridingTransactionProvider, ct)
                        .ConfigureAwait(continueOnCapturedContext);
                },
                requestContext,
                continueOnCapturedContext, 
                cancellationToken
            ).ConfigureAwait(continueOnCapturedContext);

            if (!written)
                throw new ChannelFailureException($"Could not write request {message.Id} to the outbox");
        }

        /// <summary>
        /// Adds a message to the outbox
        /// </summary>
        /// <param name="message">The message we intend to send</param>
        /// <param name="overridingTransactionProvider">A transaction provider that gives us the transaction to use with the Outbox</param>
        /// <param name="requestContext">The context of the request pipeline</param>
        /// <exception cref="ChannelFailureException">Thrown if we fail to write all the messages</exception>
        public void AddToOutbox(
            TMessage message,
            RequestContext requestContext,
            IAmABoxTransactionProvider<TTransaction> overridingTransactionProvider = null
        )
        {
            CheckOutboxOutstandingLimit();

            BrighterTracer.WriteOutboxEvent(OutboxDbOperation.Add, message, requestContext.Span, 
                overridingTransactionProvider != null, false, _instrumentationOptions); 
 
            var written = Retry(() => 
                { _outBox.Add(message, requestContext, _outboxTimeout, overridingTransactionProvider); }, 
                requestContext
            );

            if (!written)
                throw new ChannelFailureException($"Could not write message {message.Id} to the outbox");

        }

        /// <summary>
        /// Archive Message from the outbox to the outbox archive provider
        /// Throws any archiving exception
        /// </summary>
        /// <param name="millisecondsDispatchedSince">Minimum age in hours</param>
        /// <param name="requestContext">The request context for the pipeline</param>
        public void Archive(int millisecondsDispatchedSince, RequestContext requestContext)
        {
            try
            {
                var messages = _outBox
                    .DispatchedMessages(millisecondsDispatchedSince, requestContext, _archiveBatchSize)
                    .ToArray();

                s_logger.LogInformation(
                    "Found {NumberOfMessageArchived} message to archive, batch size : {BatchSize}",
                    messages.Count(), _archiveBatchSize
                );

                if (messages.Length <= 0) return;

                foreach (var message in messages)
                {
                    _archiveProvider.ArchiveMessage(message);
                }

                _outBox.Delete(messages.Select(e => e.Id).ToArray(), requestContext);

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
        /// <param name="millisecondsDispatchedSince"></param>
        /// <param name="requestContext"></param>
        /// <param name="cancellationToken">The Cancellation Token</param>
        public async Task ArchiveAsync(int millisecondsDispatchedSince, RequestContext requestContext, CancellationToken cancellationToken)
        {
            try
            {
                var messages = (await _asyncOutbox.DispatchedMessagesAsync(
                    millisecondsDispatchedSince, requestContext, pageSize: _archiveBatchSize, cancellationToken: cancellationToken
                )).ToArray();

                if (messages.Length <= 0) return;
                
                foreach (var message in messages)
                {
                    await _archiveProvider.ArchiveMessageAsync(message, cancellationToken);
                } 

                await _asyncOutbox.DeleteAsync(messages.Select(e => e.Id).ToArray(), requestContext, 
                    cancellationToken: cancellationToken
                );
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
        /// <param name="requestContext">The context of the request pipeline</param>        
        /// <typeparam name="T">The type of the call</typeparam>
        /// <typeparam name="TResponse">The type of the response</typeparam>
        public void CallViaExternalBus<T, TResponse>(Message outMessage, RequestContext requestContext)
            where T : class, ICall where TResponse : class, IResponse
        {
            //We assume that this only occurs over a blocking producer
            var producer = _producerRegistry.LookupBy(outMessage.Header.Topic);
            if (producer is IAmAMessageProducerSync producerSync)
                Retry(
                    () => producerSync.Send(outMessage),
                    requestContext
                );
        }

        /// <summary>
        /// This is the clear outbox for explicit clearing of messages.
        /// </summary>
        /// <param name="posts">The ids of the posts that you would like to clear</param>
        /// <param name="requestContext">The request context for the pipeline</param>
        /// <param name="args">For outboxes that require additional parameters such as topic, provide an optional arg</param>
        /// <exception cref="InvalidOperationException">Thrown if there is no async outbox defined</exception>
        /// <exception cref="NullReferenceException">Thrown if a message cannot be found</exception>
        public void ClearOutbox(
            string[] posts, 
            RequestContext requestContext, 
            Dictionary<string, object> args = null
        )
        {
            if (!HasOutbox())
                throw new InvalidOperationException("No outbox defined.");

            // Only allow a single Clear to happen at a time
            s_clearSemaphoreToken.Wait();
            var parentSpan = requestContext.Span;
            
            var childSpans = new Dictionary<string, Activity>();
            try
            {
                foreach (var messageId in posts)
                {
                    var span = _tracer.CreateClearSpan(CommandProcessorSpanOperation.Clear, requestContext.Span, messageId, _instrumentationOptions);
                    childSpans.Add(messageId, span);
                    requestContext.Span = span;

                    var message = _outBox.Get(messageId, requestContext);
                    if (message == null || message.Header.MessageType == MessageType.MT_NONE)
                        throw new NullReferenceException($"Message with Id {messageId} not found in the Outbox");
                    
                    BrighterTracer.WriteOutboxEvent(OutboxDbOperation.Get, message, span, false, false, _instrumentationOptions);

                    Dispatch(new[] { message }, requestContext, args);
                    requestContext.Span = parentSpan;
                }
            }
            finally
            {
                _tracer.EndSpans(childSpans);
                requestContext.Span = parentSpan;
                s_clearSemaphoreToken.Release();
            }

            CheckOutstandingMessages(requestContext);
        }

        /// <summary>
        /// This is the clear outbox for explicit clearing of messages.
        /// </summary>
        /// <param name="posts">The ids of the posts that you would like to clear</param>
        /// <param name="continueOnCapturedContext">Should we use the same thread in the callback</param>
        /// <param name="requestContext">The request context for the pipeline</param>
        /// <param name="args">For outboxes that require additional parameters such as topic, provide an optional arg</param>
        /// <param name="cancellationToken">Allow cancellation of the operation</param>
        /// <exception cref="InvalidOperationException">Thrown if there is no async outbox defined</exception>
        /// <exception cref="NullReferenceException">Thrown if a message cannot be found</exception>
        public async Task ClearOutboxAsync(
            IEnumerable<string> posts,
            RequestContext requestContext,
            bool continueOnCapturedContext = false,
            Dictionary<string, object> args = null,
            CancellationToken cancellationToken = default
        )
        {
            if (!HasAsyncOutbox())
                throw new InvalidOperationException("No async outbox defined.");

            await s_clearSemaphoreToken.WaitAsync(cancellationToken);
            var parentSpan = requestContext.Span;
            
            var childSpans = new Dictionary<string, Activity>();
            try
            {
                foreach (var messageId in posts)
                {
                    var span= _tracer.CreateClearSpan(CommandProcessorSpanOperation.Clear, requestContext.Span, messageId, _instrumentationOptions);                   
                    childSpans.Add(messageId, span);
                    requestContext.Span = span;
                    
                    var message = await _asyncOutbox.GetAsync(messageId, requestContext, _outboxTimeout, args, cancellationToken);
                    if (message == null || message.Header.MessageType == MessageType.MT_NONE)
                        throw new NullReferenceException($"Message with Id {messageId} not found in the Outbox");
                    
                    BrighterTracer.WriteOutboxEvent(OutboxDbOperation.Get, message, span, false, true, _instrumentationOptions);

                    await DispatchAsync(new[] { message }, requestContext, continueOnCapturedContext, cancellationToken);
                    requestContext.Span = parentSpan;
                }
            }
            finally
            {
                _tracer.EndSpans(childSpans);
                requestContext.Span = parentSpan;
                s_clearSemaphoreToken.Release();
            }

            CheckOutstandingMessages(requestContext);
        }

        /// <summary>
        /// This is the clear outbox for explicit clearing of messages.
        /// </summary>
        /// <param name="amountToClear">Maximum number to clear.</param>
        /// <param name="minimumAge">The minimum age of messages to be cleared in milliseconds.</param>
        /// <param name="useBulk">Use bulk sending capability of the message producer, this must be paired with useAsync.</param>
        /// <param name="requestContext">The request context for the pipeline</param>
        /// <param name="args">Optional bag of arguments required by an outbox implementation to sweep</param>
        public void ClearOustandingFromOutbox(int amountToClear,
            int minimumAge,
            bool useBulk,
            RequestContext requestContext,
            Dictionary<string, object> args = null)
        {
            if (HasAsyncOutbox())
            {
                Task.Run(() => 
                        BackgroundDispatchUsingAsync(amountToClear, minimumAge, useBulk, requestContext, args),
                        CancellationToken.None
                );
            }
            else if (HasOutbox())
            {
                Task.Run(() => 
                    BackgroundDispatchUsingSync(amountToClear, minimumAge, requestContext, args)
                );
            }
            else
            {
                throw new InvalidOperationException("No outbox defined."); 
            }
        }

        /// <summary>
        /// Given a request, run the transformation pipeline to create a message
        /// </summary>
        /// <param name="request">The request</param>
        /// <param name="requestContext">The context of the request pipeline</param>
        /// <typeparam name="TRequest">the type of the request</typeparam>
        /// <returns></returns>
        public Message CreateMessageFromRequest<TRequest>(TRequest request, RequestContext requestContext) 
            where TRequest : class, IRequest
        {
            var message = MapMessage(request, requestContext);
            return message;
        }

        /// <summary>
        /// Given a request, run the transformation pipeline to create a message 
        /// </summary>
        /// <param name="request">The request</param>
        /// <param name="requestContext">The context of the request pipeline</param>
        /// <param name="cancellationToken">Cancel the in-flight operation</param>
        /// <typeparam name="TRequest">The type of the request</typeparam>
        /// <returns></returns>
        public async Task<Message> CreateMessageFromRequestAsync<TRequest>(
            TRequest request,
            RequestContext requestContext,
            CancellationToken cancellationToken
        ) where TRequest : class, IRequest
        {
            Message message = await MapMessageAsync(request, requestContext, cancellationToken);
            return message;
        }

        /// <summary>
        /// Intended for usage with the CommandProcessor's Call method, this method will create a request from a message
        /// </summary>
        /// <param name="message">The message that forms a reply to a call</param>
        /// <param name="request">The request constructed from that message</param>
        /// <param name="requestContext">The context of the request pipeline</param>
        /// <typeparam name="TRequest">The type of the request</typeparam>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if there is no message mapper for the request</exception>
        public void CreateRequestFromMessage<TRequest>(Message message, RequestContext requestContext, out TRequest request)
            where TRequest : class, IRequest
        {
            if (_transformPipelineBuilderAsync.HasPipeline<TRequest>())
            {
                request = _transformPipelineBuilderAsync
                    .BuildUnwrapPipeline<TRequest>()
                    .UnwrapAsync(message, requestContext)
                    .GetAwaiter()
                    .GetResult();
            }
            else if (_transformPipelineBuilder.HasPipeline<TRequest>())
            {
                request = _transformPipelineBuilder
                    .BuildUnwrapPipeline<TRequest>()
                    .Unwrap(message, requestContext);
            } 
            else
            {
                throw new ArgumentOutOfRangeException(nameof(request),"No message mapper defined for request");
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

        private async Task BackgroundDispatchUsingSync(
            int amountToClear, 
            int millisecondsSinceSent,
            RequestContext requestContext,
            Dictionary<string, object> args
        )
        {
            WaitHandle[] clearTokens = new WaitHandle[2];
            clearTokens[0] = s_backgroundClearSemaphoreToken.AvailableWaitHandle;
            clearTokens[1] = s_clearSemaphoreToken.AvailableWaitHandle;
            if (WaitHandle.WaitAll(clearTokens, TimeSpan.Zero))
            {
                var parentSpan = requestContext.Span;
                var span= _tracer.CreateClearSpan(CommandProcessorSpanOperation.Clear, requestContext.Span, null, _instrumentationOptions);                   

                try
                {
                    requestContext.Span = span;
                    
                    var messages = _outBox.OutstandingMessages(millisecondsSinceSent, 
                        requestContext, amountToClear, args: args
                    ).ToArray();

                    requestContext.Span = parentSpan;
                    
                    s_logger.LogInformation("Found {NumberOfMessages} to clear out of amount {AmountToClear}",
                        messages.Count(), amountToClear);
                    
                    BrighterTracer.WriteOutboxEvent(OutboxDbOperation.OutStandingMessages, messages, span, false, false, _instrumentationOptions);
                    
                    Dispatch(messages, requestContext, args);
                    
                    s_logger.LogInformation("Messages have been cleared");
                }
                catch (Exception e)
                {
                    requestContext.Span?.SetStatus(ActivityStatusCode.Error, "Error while dispatching from outbox");
                    s_logger.LogError(e, "Error while dispatching from outbox");
                }
                finally
                {
                    _tracer.EndSpan(span);
                    s_clearSemaphoreToken.Release();
                    s_backgroundClearSemaphoreToken.Release();
                }

                CheckOutstandingMessages(requestContext);
            }
            else
            {
                requestContext.Span?.SetStatus(ActivityStatusCode.Error);
                s_logger.LogInformation("Skipping dispatch of messages as another thread is running");
            }
        }
        
        private async Task BackgroundDispatchUsingAsync(
            int amountToClear, 
            int milliSecondsSinceSent, 
            bool useBulk,
            RequestContext requestContext,
            Dictionary<string, object> args
        )
        {
            WaitHandle[] clearTokens = new WaitHandle[2];
            clearTokens[0] = s_backgroundClearSemaphoreToken.AvailableWaitHandle;
            clearTokens[1] = s_clearSemaphoreToken.AvailableWaitHandle;
            if (WaitHandle.WaitAll(clearTokens, TimeSpan.Zero))
            {
                
                var parentSpan = requestContext.Span;
                var span= _tracer.CreateClearSpan(CommandProcessorSpanOperation.Clear, requestContext.Span, null, _instrumentationOptions);                   
                 try
                {
                   requestContext.Span = span;
                    
                    var messages =
                        (await _asyncOutbox.OutstandingMessagesAsync(milliSecondsSinceSent, requestContext, 
                            pageSize: amountToClear, args: args)).ToArray();
                    
                    BrighterTracer.WriteOutboxEvent(OutboxDbOperation.OutStandingMessages, messages, span, false, true, _instrumentationOptions);
                    
                    requestContext.Span = parentSpan;

                    s_logger.LogInformation("Found {NumberOfMessages} to clear out of amount {AmountToClear}",
                        messages.Count(), amountToClear);

                    if (useBulk)
                    {
                        await BulkDispatchAsync(messages, requestContext, CancellationToken.None);
                    }
                    else
                    {
                        await DispatchAsync(messages, requestContext,false, CancellationToken.None);
                    }

                    s_logger.LogInformation("Messages have been cleared");
                }
                catch (Exception e)
                {
                    s_logger.LogError(e, "Error while dispatching from outbox");
                    requestContext.Span?.SetStatus(ActivityStatusCode.Error, "Error while dispatching from outbox");
                }
                finally
                {
                    _tracer.EndSpan(span);
                    s_clearSemaphoreToken.Release();
                    s_backgroundClearSemaphoreToken.Release();
                }

                CheckOutstandingMessages(requestContext);
            }
            else
            {
                requestContext.Span?.SetStatus(ActivityStatusCode.Error);
                s_logger.LogInformation("Skipping dispatch of messages as another thread is running");
            }
        }
        
        private void CheckOutboxOutstandingLimit()
        {
            bool hasOutBox = (_outBox != null || _asyncOutbox != null);
            if (!hasOutBox)
                return;

            s_logger.LogDebug("Outbox outstanding message count is: {OutstandingMessageCount}", _outStandingCount);
            // Because a thread recalculates this, we may always be in a delay, so we check on entry for the next outstanding item
            bool exceedsOutstandingMessageLimit =
                _maxOutStandingMessages != -1 && _outStandingCount > _maxOutStandingMessages;

            if (exceedsOutstandingMessageLimit)
                throw new OutboxLimitReachedException(
                    $"The outbox limit of {_maxOutStandingMessages} has been exceeded");
        }

        private void CheckOutstandingMessages(RequestContext requestContext)
        {
            var now = DateTime.UtcNow;
            var checkInterval =
                TimeSpan.FromMilliseconds(_maxOutStandingCheckIntervalMilliSeconds);


            var timeSinceLastCheck = now - _lastOutStandingMessageCheckAt;
            
            s_logger.LogDebug(
                "Time since last check is {SecondsSinceLastCheck} seconds",
                timeSinceLastCheck.TotalSeconds
            );
            
            if (timeSinceLastCheck < checkInterval)
            {
                s_logger.LogDebug($"Check not ready to run yet");
                return;
            }

            s_logger.LogDebug(
                "Running outstanding message check at {MessageCheckTime} after {SecondsSinceLastCheck} seconds wait",
                DateTime.UtcNow, timeSinceLastCheck.TotalSeconds
            );
            //This is expensive, so use a background thread
            Task.Run(() => OutstandingMessagesCheck(requestContext));
        }
        
               /// <summary>
        /// Configure the callbacks for the producers 
        /// </summary>
        private void ConfigureCallbacks(RequestContext requestContext)
        {
            //Only register one, to avoid two callbacks where we support both interfaces on a producer
            foreach (var producer in _producerRegistry.Producers)
            {
                if (!ConfigurePublisherCallbackMaybe(producer, requestContext))
                    ConfigureAsyncPublisherCallbackMaybe(producer, requestContext);
            }
        }

        /// <summary>
        /// If a producer supports a callback then we can use this to mark a message as dispatched in an asynchronous
        /// Outbox
        /// </summary>
        /// <param name="producer">The producer to add a callback for</param>
        /// <param name="requestContext">The request context for the pipeline</param>        
        /// <returns></returns>
        private void ConfigureAsyncPublisherCallbackMaybe(IAmAMessageProducer producer, RequestContext requestContext)
        {
            if (producer is ISupportPublishConfirmation producerSync)
            {
                producerSync.OnMessagePublished += async delegate(bool success, string id)
                {
                    if (success)
                    {
                        s_logger.LogInformation("Sent message: Id:{Id}", id);
                        if (_asyncOutbox != null)
                            await RetryAsync(
                                async ct => 
                                    await _asyncOutbox.MarkDispatchedAsync(id, requestContext, DateTime.UtcNow, cancellationToken: ct),
                                requestContext
                        );
                    }
                };
            }
        }

        /// <summary>
        /// If a producer supports a callback then we can use this to mark a message as dispatched in a synchronous
        /// Outbox
        /// </summary>
        /// <param name="producer">The producer to add a callback for</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>        
        private bool ConfigurePublisherCallbackMaybe(IAmAMessageProducer producer, RequestContext requestContext)
        {
            if (producer is ISupportPublishConfirmation producerSync)
            {
                producerSync.OnMessagePublished += delegate(bool success, string id)
                {
                    if (success)
                    {
                        s_logger.LogInformation("Sent message: Id:{Id}", id);
                        
                        if (_outBox != null)
                            Retry(
                                () => _outBox.MarkDispatched(id, requestContext, DateTime.UtcNow),
                                requestContext);
                    }
                };
                return true;
            }

            return false;
        }
        
        private void Dispatch(IEnumerable<Message> posts, RequestContext requestContext, Dictionary<string, object> args = null)
        {
            var parentSpan = requestContext.Span;
            var producerSpans = new Dictionary<string, Activity>();
            try
            {
                foreach (var message in posts)
                {
                    s_logger.LogInformation(
                        "Decoupled invocation of message: Topic:{Topic} Id:{Id}", message.Header.Topic,
                        message.Id
                    );

                    var producer = _producerRegistry.LookupBy(message.Header.Topic);
                    var span = _tracer.CreateProducerSpan(producer.Publication, message, requestContext.Span, _instrumentationOptions);
                    producer.Span = span;
                    producerSpans.Add(message.Id, span);

                    if (producer is IAmAMessageProducerSync producerSync)
                    {
                        if (producer is ISupportPublishConfirmation)
                        {
                            //mark dispatch handled by a callback - set in constructor
                            Retry(
                                () => { producerSync.Send(message); },
                                requestContext);
                        }
                        else
                        {
                            var sent = Retry(
                                () => { producerSync.Send(message); },
                                requestContext
                            );
                            if (sent)
                                Retry(
                                    () => _outBox.MarkDispatched(message.Id, requestContext, DateTime.UtcNow, args),
                                    requestContext
                                );
                        }
                    }
                    else
                        throw new InvalidOperationException("No sync message producer defined.");
                    
                    Activity.Current = parentSpan;
                    producer.Span = null;
                }
            }
            finally
            {
                _tracer.EndSpans(producerSpans);
            }
        }
        
        private async Task BulkDispatchAsync(IEnumerable<Message> posts, RequestContext requestContext, CancellationToken cancellationToken)
        {
            var parentSpan = requestContext.Span;
            var producerSpans = new Dictionary<string, Activity>();
            
            //Chunk into Topics
            try
            {
                var messagesByTopic = posts.GroupBy(m => m.Header.Topic);

                foreach (var topicBatch in messagesByTopic)
                {
                    var producer = _producerRegistry.LookupBy(topicBatch.Key);
                    var span = _tracer.CreateProducerSpan(producer.Publication, null, requestContext.Span, _instrumentationOptions);
                    producer.Span = span;
                    producerSpans.Add(topicBatch.Key, span);

                    if (producer is IAmABulkMessageProducerAsync bulkMessageProducer)
                    {
                        var messages = topicBatch.ToArray();
                    
                        s_logger.LogInformation("Bulk Dispatching {NumberOfMessages} for Topic {TopicName}",
                            messages.Length, topicBatch.Key
                        );
                    
                    
                        var dispatchesMessages = bulkMessageProducer.SendAsync(messages, cancellationToken);

                        await foreach (var successfulMessage in dispatchesMessages)
                        {
                            if (!(producer is ISupportPublishConfirmation))
                            {
                                await RetryAsync(async _ => 
                                        await _asyncOutbox.MarkDispatchedAsync(
                                            successfulMessage, requestContext, DateTime.UtcNow, cancellationToken: cancellationToken
                                        ),
                                    requestContext,
                                    cancellationToken: cancellationToken
                                );
                            }
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException("No async bulk message producer defined.");
                    }
                }
            }
            finally
            {
                _tracer.EndSpans(producerSpans);
                requestContext.Span = parentSpan;
            }
        }
        
        private async Task DispatchAsync(
            IEnumerable<Message> posts,
            RequestContext requestContext,
            bool continueOnCapturedContext,
            CancellationToken cancellationToken)
        {
            var parentSpan = requestContext.Span;
            var producerSpans = new Dictionary<string, Activity>();

            try
            {
                foreach (var message in posts)
                {
                    s_logger.LogInformation(
                        "Decoupled invocation of message: Topic:{Topic} Id:{Id}",
                        message.Header.Topic, message.Id
                    ); 
                
                    var producer = _producerRegistry.LookupBy(message.Header.Topic);
                    var span = _tracer.CreateProducerSpan(producer.Publication, message, parentSpan, _instrumentationOptions);
                    producer.Span = span;
                    producerSpans.Add(message.Id, span);

                    if (producer is IAmAMessageProducerAsync producerAsync)
                    {
                        if (producer is ISupportPublishConfirmation)
                        {
                            //mark dispatch handled by a callback - set in constructor
                            await RetryAsync(
                                    async _ =>
                                        await producerAsync.SendAsync(message).ConfigureAwait(continueOnCapturedContext),
                                    requestContext,
                                    continueOnCapturedContext,
                                    cancellationToken)
                                .ConfigureAwait(continueOnCapturedContext);
                        }
                        else
                        {
                            var sent = await RetryAsync(
                                    async _ => await producerAsync.SendAsync(message).ConfigureAwait(continueOnCapturedContext),
                                    requestContext,
                                    continueOnCapturedContext,
                                    cancellationToken
                                )
                                .ConfigureAwait(continueOnCapturedContext
                                );

                            if (sent)
                                await RetryAsync(
                                    async _ => await _asyncOutbox.MarkDispatchedAsync(
                                        message.Id, requestContext, DateTime.UtcNow, cancellationToken: cancellationToken
                                    ),
                                    requestContext,
                                    cancellationToken: cancellationToken
                                );
                        }
                    }
                    else
                        throw new InvalidOperationException("No async message producer defined.");
                }
            }
            finally
            {
                _tracer.EndSpans(producerSpans); 
                requestContext.Span = parentSpan;
            }
        }
        
        private Message MapMessage<TRequest>(TRequest request, RequestContext requestContext)
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
                    .Wrap(request, requestContext, publication);
            }                                                
            else
            {
                throw new ArgumentOutOfRangeException(nameof(request), "No message mapper defined for request");
            }

            return message;
        }

        private async Task<Message> MapMessageAsync<TRequest>(
            TRequest request, 
            RequestContext requestContext, 
            CancellationToken cancellationToken
        )
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
                    .WrapAsync(request, requestContext, publication, cancellationToken);
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(request), "No message mapper defined for request");
            }

            return message;
        }

        private void OutstandingMessagesCheck(RequestContext requestContext)
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
                            requestContext,
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
        
        private bool Retry(Action action, RequestContext requestContext)
        {
            var policy = _policyRegistry.Get<Policy>(CommandProcessor.RETRYPOLICY);
            var result = policy.ExecuteAndCapture(action);
            if (result.Outcome != OutcomeType.Successful)
            {
                if (result.FinalException != null)
                {
                    s_logger.LogError(result.FinalException, "Exception whilst trying to publish message");
                    CheckOutstandingMessages(requestContext);
                }

                return false;
            }

            return true;
        }

        private async Task<bool> RetryAsync(
            Func<CancellationToken, Task> send, 
            RequestContext requestContext,
            bool continueOnCapturedContext = false,
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
                    CheckOutstandingMessages(requestContext);
                }

                return false;
            }

            return true;
        }
    }
}
