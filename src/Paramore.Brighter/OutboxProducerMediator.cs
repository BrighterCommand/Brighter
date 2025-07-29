#region Licence
/* The MIT License (MIT)
Copyright © 2022 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Scheduler.Events;
using Polly;
using Polly.Registry;

// ReSharper disable StaticMemberInGenericType

namespace Paramore.Brighter
{
    /// <summary>
    /// Mediates the interaction between a producer and an outbox. As we want to write to the outbox, and then send from there
    /// to the producer, we need to take control of produce operations to mediate between the two in a transaction.
    /// NOTE: This class is singleton. The CommandProcessor by contrast, is transient or more typically scoped. 
    /// </summary>
    public partial class OutboxProducerMediator<TMessage, TTransaction> : IAmAnOutboxProducerMediator,
        IAmAnOutboxProducerMediator<TMessage, TTransaction>
        where TMessage : Message
    {
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<CommandProcessor>();

        private readonly IPolicyRegistry<string> _policyRegistry;
        private readonly TransformPipelineBuilder _transformPipelineBuilder;
        private readonly TransformPipelineBuilderAsync _transformPipelineBuilderAsync;
        private readonly IAmAnOutboxSync<TMessage, TTransaction>? _outBox;
        private readonly IAmAnOutboxAsync<TMessage, TTransaction>? _asyncOutbox;
        private readonly int _outboxTimeout;
        private readonly IAmAProducerRegistry _producerRegistry;
        private readonly InstrumentationOptions _instrumentationOptions;
        private readonly IAmAPublicationFinder _publicationFinder;
        private readonly IAmAnOutboxCircuitBreaker? _outboxCircuitBreaker;
        private readonly Dictionary<string, List<TMessage>> _outboxBatches = new();

        private static readonly SemaphoreSlim s_clearSemaphoreToken = new(1, 1);

        private static readonly SemaphoreSlim s_backgroundClearSemaphoreToken = new(1, 1);

        //Used to checking the limit on outstanding messages for an Outbox. We throw at that point. Writes to the static
        //bool should be made thread-safe by locking the object
        private static readonly SemaphoreSlim s_checkOutstandingSemaphoreToken = new(1, 1);

        private DateTimeOffset _lastOutStandingMessageCheckAt;

        private const string NoSyncOutboxError = "A sync Outbox must be defined.";
        private const string NoAsyncOutboxError = "An async Outbox must be defined.";
            
        //Uses -1 to indicate no outbox and will thus force a throw on a failed publish
        private int _outStandingCount;
        private bool _disposed;
        private readonly int _maxOutStandingMessages;
        private readonly TimeSpan _maxOutStandingCheckInterval;
        private readonly Dictionary<string, object> _outBoxBag;
        private readonly IAmABrighterTracer? _tracer;
        private readonly TimeProvider _timeProvider;

        /// <summary>
        /// Creates an instance of the Outbox Producer Mediator
        /// </summary>
        /// <param name="producerRegistry">A registry of producers</param>
        /// <param name="policyRegistry">A registry for reliability policies</param>
        /// <param name="mapperRegistry">The mapper registry; it should also implement IAmAMessageMapperRegistryAsync</param>
        /// <param name="messageTransformerFactory">The factory used to create a transformer pipeline for a message mapper</param>
        /// <param name="messageTransformerFactoryAsync">The factory used to create a transformer pipeline for an async message mapper</param>
        /// <param name="tracer"></param>
        /// <param name="publicationFinder">A publication finder.</param>
        /// <param name="outboxCircuitBreaker">Track unhealthy topics and allow for cooldown, should be registered as singleton and shared with Outbox</param>
        /// <param name="outbox">An outbox for transactional messaging, if none is provided, use an InMemoryOutbox</param>
        /// <param name="requestContextFactory"></param>
        /// <param name="outboxTimeout">How long to timeout for with an outbox</param>
        /// <param name="maxOutStandingMessages">How many messages can become outstanding in the Outbox before we throw an OutboxLimitReached exception</param>
        /// <param name="maxOutStandingCheckInterval">How long before we check for maxOutStandingMessages</param>
        /// <param name="outBoxBag">An outbox may require additional arguments, such as a topic list to search</param>
        /// <param name="timeProvider"></param>
        /// <param name="instrumentationOptions">How verbose do we want our instrumentation to be</param>
        public OutboxProducerMediator(
            IAmAProducerRegistry producerRegistry,
            IPolicyRegistry<string> policyRegistry,
            IAmAMessageMapperRegistry mapperRegistry,
            IAmAMessageTransformerFactory messageTransformerFactory,
            IAmAMessageTransformerFactoryAsync messageTransformerFactoryAsync,
            IAmABrighterTracer? tracer, 
            IAmAPublicationFinder publicationFinder,
            IAmAnOutbox? outbox = null,
            IAmAnOutboxCircuitBreaker? outboxCircuitBreaker = null,
            IAmARequestContextFactory? requestContextFactory = null,
            int outboxTimeout = 300,
            int maxOutStandingMessages = -1,
            TimeSpan? maxOutStandingCheckInterval = null,
            Dictionary<string, object>? outBoxBag = null,
            TimeProvider? timeProvider = null,
            InstrumentationOptions instrumentationOptions = InstrumentationOptions.All)
        {
            _producerRegistry = producerRegistry ??
                                throw new ConfigurationException("Missing Producer Registry for External Bus Services");
            _policyRegistry = policyRegistry ??
                              throw new ConfigurationException("Missing Policy Registry for External Bus Services");

            requestContextFactory ??= new InMemoryRequestContextFactory();

            if (mapperRegistry is null)
                throw new ConfigurationException(
                    "A Command Processor with an external bus must have a message mapper registry that implements IAmAMessageMapperRegistry");
            if (mapperRegistry is not IAmAMessageMapperRegistryAsync mapperRegistryAsync)
                throw new ConfigurationException(
                    "A Command Processor with an external bus must have a message mapper registry that implements IAmAMessageMapperRegistryAsync");
            if (messageTransformerFactory is null || messageTransformerFactoryAsync is null)
                throw new ConfigurationException(
                    "A Command Processor with an external bus must have a message transformer factory");
            
            _timeProvider = timeProvider ?? TimeProvider.System;
            _lastOutStandingMessageCheckAt = _timeProvider.GetUtcNow();

            _transformPipelineBuilder = new TransformPipelineBuilder(mapperRegistry, messageTransformerFactory, instrumentationOptions);
            _transformPipelineBuilderAsync =
                new TransformPipelineBuilderAsync(mapperRegistryAsync, messageTransformerFactoryAsync, instrumentationOptions);

            //default to in-memory; expectation for an in memory box is Message and CommittableTransaction
            outbox ??= new InMemoryOutbox(TimeProvider.System);
            outbox.Tracer = tracer;

            if (outbox is IAmAnOutboxSync<TMessage, TTransaction> syncOutbox) _outBox = syncOutbox;
            if (outbox is IAmAnOutboxAsync<TMessage, TTransaction> asyncOutbox) _asyncOutbox = asyncOutbox;
            _outboxCircuitBreaker = outboxCircuitBreaker;

            _outboxTimeout = outboxTimeout;
            _maxOutStandingMessages = maxOutStandingMessages;
            _maxOutStandingCheckInterval = maxOutStandingCheckInterval ?? TimeSpan.FromMilliseconds(1000);
            _outBoxBag = outBoxBag ?? new Dictionary<string, object>();
            _instrumentationOptions = instrumentationOptions;
            _tracer = tracer;
            _publicationFinder = publicationFinder;

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

            if (disposing)
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
        /// <param name="batchId">The id of the deposit batch, if this isn't set items will be added to the outbox as they come in and not as a batch</param>
        /// <typeparam name="TTransaction">The type of the transaction used to add to the Outbox</typeparam>
        /// <exception cref="ChannelFailureException">Thrown if we cannot write to the Outbox</exception>
        public async Task AddToOutboxAsync(
            TMessage message,
            RequestContext requestContext,
            IAmABoxTransactionProvider<TTransaction>? overridingTransactionProvider = null,
            bool continueOnCapturedContext = true,
            CancellationToken cancellationToken = default,
            string? batchId = null)
        {
            if (_asyncOutbox is null) throw new ArgumentException(NoAsyncOutboxError);
            
            if (batchId != null)
            {
                _outboxBatches[batchId].Add(message);
                return;
            }

            CheckOutboxOutstandingLimit();

            BrighterTracer.WriteOutboxEvent(BoxDbOperation.Add, message, requestContext.Span,
                overridingTransactionProvider != null, true, _instrumentationOptions);

            var written = await RetryAsync(
                async ct =>
                {
                    await _asyncOutbox
                        .AddAsync(message, requestContext, _outboxTimeout, overridingTransactionProvider, ct)
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
        /// <param name="batchId">The id of the deposit batch, if this isn't set items will be added to the outbox as they come in and not as a batch</param>
        /// <exception cref="ChannelFailureException">Thrown if we fail to write all the messages</exception>
        public void AddToOutbox(
            TMessage message,
            RequestContext requestContext,
            IAmABoxTransactionProvider<TTransaction>? overridingTransactionProvider = null,
            string? batchId = null
        )
        {
            if (_outBox is null) throw new ArgumentException(NoSyncOutboxError);
            if (batchId != null)
            {
                _outboxBatches[batchId].Add(message);
                return;
            }

            CheckOutboxOutstandingLimit();

            BrighterTracer.WriteOutboxEvent(BoxDbOperation.Add, message, requestContext.Span,
                overridingTransactionProvider != null, false, _instrumentationOptions);

            var written = Retry(() =>
                {
                    _outBox.Add(message, requestContext, _outboxTimeout, overridingTransactionProvider);
                },
                requestContext
            );

            if (!written)
                throw new ChannelFailureException($"Could not write message {message.Id} to the outbox");
        }

        /// <summary>
        /// Used with RPC to call a remote service via the external bus
        /// </summary>
        /// <param name="outMessage">The message to send</param>
        /// <param name="requestContext">The context of the request pipeline</param>        
        /// <typeparam name="T">The type of the call</typeparam>
        /// <typeparam name="TResponse">The type of the response</typeparam>
        public void CallViaExternalBus<T, TResponse>(Message outMessage, RequestContext? requestContext)
            where T : class, ICall where TResponse : class, IResponse
        {
            //We assume that this only occurs over a blocking producer
            var producer = _producerRegistry.LookupSyncBy(outMessage.Header.Topic);
                Retry(
                    () => producer.Send(outMessage),
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
            Id[] posts,
            RequestContext requestContext,
            Dictionary<string, object>? args = null
        )
        {
            if (!HasOutbox())
                throw new InvalidOperationException("No outbox defined.");

            // Only allow a single Clear to happen at a time
            s_clearSemaphoreToken.Wait();
            var parentSpan = requestContext.Span;

            var childSpans = new ConcurrentDictionary<string, Activity>();
            try
            {
                if (_outBox is null) throw new ArgumentException(NoSyncOutboxError);
                foreach (var messageId in posts)
                {
                    var span = _tracer?.CreateClearSpan(CommandProcessorSpanOperation.Clear, requestContext.Span,
                        messageId, _instrumentationOptions);
                    if (span is not null)
                    {
                        childSpans.TryAdd(messageId, span);
                        requestContext.Span = span;
                    }
                    
                    var message = _outBox.Get(messageId, requestContext);
                    if (message is null || message.Header.MessageType == MessageType.MT_NONE)
                        throw new NullReferenceException($"Message with Id {messageId} not found in the Outbox");

                    BrighterTracer.WriteOutboxEvent(BoxDbOperation.Get, message, span, false, false,
                        _instrumentationOptions);

                    Dispatch(new[] { message }, requestContext, args);
                }
            }
            finally
            {
                _tracer?.EndSpans(childSpans);
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
            IEnumerable<Id> posts,
            RequestContext requestContext,
            bool continueOnCapturedContext = true,
            Dictionary<string, object>? args = null,
            CancellationToken cancellationToken = default
        )
        {
            if (!HasAsyncOutbox())
                throw new InvalidOperationException("No async outbox defined.");

            await s_clearSemaphoreToken.WaitAsync(cancellationToken);
            var parentSpan = requestContext.Span;

            var childSpans = new ConcurrentDictionary<string, Activity>();
            try
            {
                if(_asyncOutbox is null)throw new ArgumentException(NoAsyncOutboxError);
                foreach (var messageId in posts)
                {
                    var span = _tracer?.CreateClearSpan(CommandProcessorSpanOperation.Clear, requestContext.Span,
                        messageId, _instrumentationOptions);
                    if (span != null) childSpans.TryAdd(messageId, span);
                    requestContext.Span = span;

                    var message = await _asyncOutbox.GetAsync(messageId, requestContext, _outboxTimeout, args,
                        cancellationToken);
                    if (message is null || message.Header.MessageType == MessageType.MT_NONE)
                        throw new NullReferenceException($"Message with Id {messageId} not found in the Outbox");

                    BrighterTracer.WriteOutboxEvent(BoxDbOperation.Get, message, requestContext.Span, false, true,
                        _instrumentationOptions);

                    await DispatchAsync(new[] { message }, requestContext, continueOnCapturedContext,
                        cancellationToken);
                }
            }
            finally
            {
                _tracer?.EndSpans(childSpans);
                requestContext.Span = parentSpan;
                s_clearSemaphoreToken.Release();
            }

            CheckOutstandingMessages(requestContext);
        }

        /// <summary>
        /// This is the clear outbox for explicit clearing of messages. It runs a task in the background to clear the outbox.
        /// This method returns whilst that thread runs, so it is non-blocking but also does not indicate the clear has
        /// happened by returning control - that happens in parallel. 
        /// </summary>
        /// <remarks>Only works for Async Outboxes</remarks>
        /// <param name="amountToClear">Maximum number to clear.</param>
        /// <param name="minimumAge">The minimum age of messages to be cleared.</param>
        /// <param name="useBulk">Use bulk sending capability of the message producer, this must be paired with useAsync.</param>
        /// <param name="requestContext">The request context for the pipeline</param>
        /// <param name="args">Optional bag of arguments required by an outbox implementation to sweep</param>
        /// <param name="cancellationToken">Cancellation Token</param>
        public async Task ClearOutstandingFromOutboxAsync(int amountToClear,
            TimeSpan minimumAge,
            bool useBulk,
            RequestContext requestContext,
            Dictionary<string, object>? args = null,
            CancellationToken cancellationToken = default)
        {
            if (HasAsyncOutbox())
                await BackgroundDispatchUsingAsync(amountToClear, minimumAge, useBulk, requestContext, args, cancellationToken);
            else
                throw new InvalidOperationException("No Async outbox defined.");
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
            // The fired scheduler message is a special case
            // Because the message is in the raw form already, just waiting to be fired
            if (request is FireSchedulerMessage scheduler)
            {
                return scheduler.Message;
            }
            
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
            // The fired scheduler message is a special case
            // Because the message is in the raw form already, just waiting to be fired
            if (request is FireSchedulerMessage schedulerMessage)
            {
                return schedulerMessage.Message;
            }
            
            var message = await MapMessageAsync(request, requestContext, cancellationToken);
            return message;
        }

        /// <summary>
        /// Intended for usage with the CommandProcessor's Call method, this method will create a request from a message
        /// Sync over async as we block on Call
        /// </summary>
        /// <param name="message">The message that forms a reply to a call</param>
        /// <param name="request">The request constructed from that message</param>
        /// <param name="requestContext">The context of the request pipeline</param>
        /// <typeparam name="TRequest">The type of the request</typeparam>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if there is no message mapper for the request</exception>
        public void CreateRequestFromMessage<TRequest>(Message message, RequestContext? requestContext,
            out TRequest request)
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
                throw new ArgumentOutOfRangeException(nameof(request), "No message mapper defined for request");
            }
        }

        /// <summary>
        /// Commence a batch of outbox messages to add
        /// </summary>
        /// <returns>The ID of the new batch</returns>
        public string StartBatchAddToOutbox()
        {
            var batchId = Uuid.NewAsString();
            _outboxBatches.Add(batchId, new List<TMessage>());
            return batchId;
        }

        public void EndBatchAddToOutbox(string batchId, IAmABoxTransactionProvider<TTransaction>? transactionProvider,
            RequestContext requestContext)
        {
            CheckOutboxOutstandingLimit();

            BrighterTracer.WriteOutboxEvent(BoxDbOperation.Add, _outboxBatches[batchId], requestContext.Span,
                transactionProvider != null, false, _instrumentationOptions);

            if (_outBox is null) throw new ArgumentException(NoSyncOutboxError);
            
            var written = Retry(() =>
                {
                    _outBox.Add(_outboxBatches[batchId], requestContext, _outboxTimeout, transactionProvider);
                },
                requestContext
            );

            if (!written)
                throw new ChannelFailureException($"Could not write batch {batchId} to the outbox");
        }

        /// <summary>
        /// Flush the batch of Messages to the outbox.
        /// </summary>
        /// <param name="batchId">The ID of the batch to be flushed</param>
        /// <param name="transactionProvider"></param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="IAmARequestContextFactory"/> </param>
        /// <param name="cancellationToken"></param>
        public async Task EndBatchAddToOutboxAsync(string batchId,
            IAmABoxTransactionProvider<TTransaction>? transactionProvider, RequestContext requestContext,
            CancellationToken cancellationToken)
        {
            CheckOutboxOutstandingLimit();

            BrighterTracer.WriteOutboxEvent(BoxDbOperation.Add, _outboxBatches[batchId], requestContext.Span,
                transactionProvider != null, true, _instrumentationOptions);

            if (_asyncOutbox is null) throw new ArgumentException(NoAsyncOutboxError);
            
            var written = await RetryAsync(
                async _ =>
                {
                    await _asyncOutbox.AddAsync(_outboxBatches[batchId], requestContext, _outboxTimeout,
                        transactionProvider, cancellationToken);
                },
                requestContext,
                cancellationToken: cancellationToken
            );

            if (!written)
                throw new ChannelFailureException($"Could not write batch {batchId} to the outbox");

            _outboxBatches.Remove(batchId);
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

        private async Task BackgroundDispatchUsingAsync(
            int amountToClear,
            TimeSpan timeSinceSent,
            bool useBulk,
            RequestContext requestContext,
            Dictionary<string, object>? args,
            CancellationToken cancellationToken
        )
        {
            WaitHandle[] clearTokens = new WaitHandle[2];
            clearTokens[0] = s_backgroundClearSemaphoreToken.AvailableWaitHandle;
            clearTokens[1] = s_clearSemaphoreToken.AvailableWaitHandle;
            _outboxCircuitBreaker?.CoolDown();

            if (WaitHandle.WaitAll(clearTokens, TimeSpan.Zero))
            {
                //NOTE: The wait handle only signals availability, still need to increment the counter:
                // see https://learn.microsoft.com/en-us/dotnet/api/System.Threading.SemaphoreSlim.AvailableWaitHandle
                await s_backgroundClearSemaphoreToken.WaitAsync(cancellationToken);
                await s_clearSemaphoreToken.WaitAsync(cancellationToken);
                
                var parentSpan = requestContext.Span;
                var span = _tracer?.CreateClearSpan(CommandProcessorSpanOperation.Clear, requestContext.Span, null,
                    _instrumentationOptions);
                try
                {
                    requestContext.Span = span;

                    if (_asyncOutbox is null) throw new ArgumentException(NoAsyncOutboxError);
                    var messages =
                        (await _asyncOutbox.OutstandingMessagesAsync(timeSinceSent, requestContext,
                            pageSize: amountToClear, trippedTopics: _outboxCircuitBreaker?.TrippedTopics, args: args, cancellationToken: cancellationToken)).ToArray();

                    BrighterTracer.WriteOutboxEvent(BoxDbOperation.OutStandingMessages, messages, span, false, true,
                        _instrumentationOptions);

                    requestContext.Span = parentSpan;

                    Log.FoundMessagesToClear(s_logger, messages.Count(), amountToClear);

                    if (useBulk)
                    {
                        await BulkDispatchAsync(messages, requestContext, cancellationToken);
                    }
                    else
                    {
                        await DispatchAsync(messages, requestContext, false, cancellationToken);
                    }

                    Log.MessagesHaveBeenCleared(s_logger);
                }
                catch (Exception e)
                {
                    Log.ErrorWhileDispatchingFromOutbox(s_logger, e);
                    requestContext.Span?.SetStatus(ActivityStatusCode.Error, "Error while dispatching from outbox");
                    throw;
                }
                finally
                {
                    _tracer?.EndSpan(span);
                    s_clearSemaphoreToken.Release();
                    s_backgroundClearSemaphoreToken.Release();
                }

                CheckOutstandingMessages(requestContext);
            }
            else
            {
                requestContext.Span?.SetStatus(ActivityStatusCode.Error);
                Log.SkippingDispatchOfMessages(s_logger);
            }
        }

        private void CheckOutboxOutstandingLimit()
        {
            bool hasOutBox = (_outBox != null || _asyncOutbox != null);
            if (!hasOutBox)
                return;

            Log.OutboxOutstandingMessageCount(s_logger, _outStandingCount);
            // Because a thread recalculates this, we may always be in a delay, so we check on entry for the next outstanding item
            bool exceedsOutstandingMessageLimit =
                _maxOutStandingMessages != -1 && _outStandingCount > _maxOutStandingMessages;

            if (exceedsOutstandingMessageLimit)
                throw new OutboxLimitReachedException(
                    $"The outbox limit of {_maxOutStandingMessages} has been exceeded");
        }

        private void CheckOutstandingMessages(RequestContext? requestContext)
        {
            var now = _timeProvider.GetUtcNow();

            var timeSinceLastCheck = now - _lastOutStandingMessageCheckAt;

            Log.TimeSinceLastCheck(s_logger, timeSinceLastCheck.TotalSeconds);

            if (timeSinceLastCheck < _maxOutStandingCheckInterval)
            {
                Log.CheckNotReadyToRunYet(s_logger);
                return;
            }                                                    

            Log.RunningOutstandingMessageCheck(s_logger, now, timeSinceLastCheck.TotalSeconds);
            //This is expensive, so use a background thread
            Task.Run(
                () => OutstandingMessagesCheck(requestContext)
            );
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
                        Log.SentMessage(s_logger, id);
                        if (_asyncOutbox != null)
                            await RetryAsync(
                                async ct =>
                                    await _asyncOutbox.MarkDispatchedAsync(id, requestContext, _timeProvider.GetUtcNow(),
                                        cancellationToken: ct),
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
                        Log.SentMessage(s_logger, id);

                        if (_outBox != null)
                            Retry(
                                () => _outBox.MarkDispatched(id, requestContext, _timeProvider.GetUtcNow()),
                                requestContext);
                    }
                };
                return true;
            }

            return false;
        }

        private void Dispatch(IEnumerable<Message> posts, 
            RequestContext requestContext,
            Dictionary<string, object>? args = null)
        {
            var parentSpan = requestContext.Span;
            var producerSpans = new ConcurrentDictionary<string, Activity>();
            try
            {
                if (_outBox is null) throw new ArgumentException(NoSyncOutboxError);
                foreach (var message in posts)
                {
                    Log.DecoupledInvocationOfMessage(s_logger, message.Header.Topic, message.Id);

                    var producer = _producerRegistry.LookupBy(message.Header.Topic);
                    var span = _tracer?.CreateProducerSpan(producer.Publication, message, requestContext.Span,
                        _instrumentationOptions);
                    producer.Span = span;
                    if (span != null) producerSpans.TryAdd(message.Id, span);

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
                                    () => _outBox.MarkDispatched(message.Id, requestContext, _timeProvider.GetUtcNow(), args),
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
                _tracer?.EndSpans(producerSpans);
            }
        }

        private async Task BulkDispatchAsync(IEnumerable<Message> posts, RequestContext requestContext,
            CancellationToken cancellationToken)
        {
            var parentSpan = requestContext.Span;
            var producerSpans = new ConcurrentDictionary<string, Activity>();

            //Chunk into Topics
            try
            {
                if (_asyncOutbox is null) throw new ArgumentException(NoAsyncOutboxError);
                var messagesByTopic = posts.GroupBy(m => m.Header.Topic);

                foreach (var topicBatch in messagesByTopic)
                {
                    var producer = _producerRegistry.LookupBy(topicBatch.Key);
                    var span = _tracer?.CreateProducerSpan(producer.Publication, null, requestContext.Span,
                        _instrumentationOptions);

                    if (span is not null)
                    {
                        producer.Span = span;
                        producerSpans.TryAdd(topicBatch.Key, span);
                    }

                    if (producer is IAmABulkMessageProducerAsync bulkMessageProducer)
                    {
                        var messages = topicBatch.ToArray();

                        Log.BulkDispatchingMessages(s_logger, messages.Length, topicBatch.Key);

                        var dispatchesMessages = bulkMessageProducer.SendAsync(messages, cancellationToken);
                        
                        await foreach (var successfulMessage in dispatchesMessages)
                        {
                            if (!(producer is ISupportPublishConfirmation))
                            {
                                await RetryAsync(async _ =>
                                        await _asyncOutbox.MarkDispatchedAsync(
                                            successfulMessage, requestContext, _timeProvider.GetUtcNow(),
                                            cancellationToken: cancellationToken
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
                _tracer?.EndSpans(producerSpans);
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
            var producerSpans = new ConcurrentDictionary<string, Activity>();

            try
            {
                if (_asyncOutbox is null) throw new ArgumentException(NoAsyncOutboxError);
                foreach (var message in posts)
                {
                    Log.DecoupledInvocationOfMessage(s_logger, message.Header.Topic, message.Id);

                    var producer = _producerRegistry.LookupBy(message.Header.Topic);
                    var span = _tracer?.CreateProducerSpan(producer.Publication, message, parentSpan,
                        _instrumentationOptions);
                    producer.Span = span;
                    if (span != null) producerSpans.TryAdd(message.Id, span);

                    if (producer is IAmAMessageProducerAsync producerAsync)
                    {
                        var sent = await RetryAsync(
                                async _ => await producerAsync.SendAsync(message, cancellationToken)
                                    .ConfigureAwait(continueOnCapturedContext),
                                requestContext,
                                continueOnCapturedContext,
                                cancellationToken
                            )
                            .ConfigureAwait(continueOnCapturedContext);

                        if (producer is not ISupportPublishConfirmation && sent)
                        {
                            await RetryAsync(
                                async _ => await _asyncOutbox.MarkDispatchedAsync(
                                    message.Id, requestContext, _timeProvider.GetUtcNow(),
                                    cancellationToken: cancellationToken
                                ),
                                requestContext,
                                cancellationToken: cancellationToken
                            );
                        }

                        if(!sent) TripTopic(message);
   
                    }
                    else
                        throw new InvalidOperationException("No async message producer defined.");
                }
            }
            finally
            {
                _tracer?.EndSpans(producerSpans);
                requestContext.Span = parentSpan;
            }
        }

        private Message MapMessage<TRequest>(TRequest request, RequestContext requestContext)
            where TRequest : class, IRequest
        {
            var publication = _publicationFinder.Find<TRequest>(_producerRegistry, requestContext);
            if (publication == null)
            {
                throw new ConfigurationException($"No publication found for request {request.GetType().Name}");
            }

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
            var publication = _publicationFinder.Find<TRequest>(_producerRegistry, requestContext);
            if (publication == null)
            {
                throw new ConfigurationException($"No publication found for request {request.GetType().Name}");
            }

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

        private void OutstandingMessagesCheck(RequestContext? requestContext)
        {
            s_checkOutstandingSemaphoreToken.Wait();

            _lastOutStandingMessageCheckAt = _timeProvider.GetUtcNow();
            Log.BeginCountOfOutstandingMessages(s_logger);
            try
            {
                if (_outBox != null)
                {
                    _outStandingCount = _outBox
                        .OutstandingMessages(
                            _maxOutStandingCheckInterval,
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
                Log.ErrorGettingOutstandingMessageCount(s_logger, ex);
                _outStandingCount = 0;
            }
            finally
            {
                Log.CurrentOutstandingCount(s_logger, _outStandingCount);
                s_checkOutstandingSemaphoreToken.Release();
            }
        }

        private bool Retry(Action action, RequestContext? requestContext)
        {
            var policy = _policyRegistry.Get<Policy>(CommandProcessor.RETRYPOLICY);
            var result = policy.ExecuteAndCapture(action);
            if (result.Outcome != OutcomeType.Successful)
            {
                if (result.FinalException != null)
                {
                    Log.ExceptionWhilstTryingToPublishMessage(s_logger, result.FinalException);
                    CheckOutstandingMessages(requestContext);
                }

                return false;
            }

            return true;
        }

        private async Task<bool> RetryAsync(
            Func<CancellationToken, Task> send,
            RequestContext? requestContext,
            bool continueOnCapturedContext = true,
            CancellationToken cancellationToken = default)
        {
            var policy = _policyRegistry.Get<AsyncPolicy>(CommandProcessor.RETRYPOLICYASYNC);
            
            var result = await policy
                .ExecuteAndCaptureAsync(send, cancellationToken, continueOnCapturedContext)
                .ConfigureAwait(continueOnCapturedContext);

            if (result.Outcome != OutcomeType.Successful)
            {
                if (result.FinalException != null)
                {
                    Log.ExceptionWhilstTryingToPublishMessage(s_logger, result.FinalException);
                    CheckOutstandingMessages(requestContext);
                }

                return false;
            }

            return true;
        }

        private void TripTopic(Message message)
        {
            if(!RoutingKey.IsNullOrEmpty(message.Header.Topic))
                _outboxCircuitBreaker?.TripTopic(message.Header.Topic);
        }
        
        private static partial class Log
        {
            [LoggerMessage(LogLevel.Information, "Found {NumberOfMessages} to clear out of amount {AmountToClear}")]
            public static partial void FoundMessagesToClear(ILogger logger, int numberOfMessages, int amountToClear);
            
            [LoggerMessage(LogLevel.Debug, "Time since last check is {SecondsSinceLastCheck} seconds")]
            public static partial void TimeSinceLastCheck(ILogger logger, double secondsSinceLastCheck);
            
            [LoggerMessage(LogLevel.Debug, "Check not ready to run yet")]
            public static partial void CheckNotReadyToRunYet(ILogger logger);
            
            [LoggerMessage(LogLevel.Debug, "Running outstanding message check at {MessageCheckTime} after {SecondsSinceLastCheck} seconds wait")]
            public static partial void RunningOutstandingMessageCheck(ILogger logger, DateTimeOffset messageCheckTime, double secondsSinceLastCheck);
            
            [LoggerMessage(LogLevel.Information, "Sent message: Id:{Id}")]
            public static partial void SentMessage(ILogger logger, string id);
            
            [LoggerMessage(LogLevel.Information, "Decoupled invocation of message: Topic:{Topic} Id:{Id}")]
            public static partial void DecoupledInvocationOfMessage(ILogger logger, string topic, string id);
            
            [LoggerMessage(LogLevel.Information, "Bulk Dispatching {NumberOfMessages} for Topic {TopicName}")]
            public static partial void BulkDispatchingMessages(ILogger logger, int numberOfMessages, string topicName);
            
            [LoggerMessage(LogLevel.Debug, "Begin count of outstanding messages")]
            public static partial void BeginCountOfOutstandingMessages(ILogger logger);
            
            [LoggerMessage(LogLevel.Error, "Error getting outstanding message count, reset count")]
            public static partial void ErrorGettingOutstandingMessageCount(ILogger logger, Exception ex);
            
            [LoggerMessage(LogLevel.Debug, "Current outstanding count is {OutstandingCount}")]
            public static partial void CurrentOutstandingCount(ILogger logger, int outstandingCount);
            
            [LoggerMessage(LogLevel.Error, "Exception whilst trying to publish message")]
            public static partial void ExceptionWhilstTryingToPublishMessage(ILogger logger, Exception exception);
            
            [LoggerMessage(LogLevel.Information, "Messages have been cleared")]
            public static partial void MessagesHaveBeenCleared(ILogger logger);
            
            [LoggerMessage(LogLevel.Error, "Error while dispatching from outbox")]
            public static partial void ErrorWhileDispatchingFromOutbox(ILogger logger, Exception exception);
            
            [LoggerMessage(LogLevel.Information, "Skipping dispatch of messages as another thread is running")]
            public static partial void SkippingDispatchOfMessages(ILogger logger);
            
            [LoggerMessage(LogLevel.Debug, "Outbox outstanding message count is: {OutstandingMessageCount}")]
            public static partial void OutboxOutstandingMessageCount(ILogger logger, int outstandingMessageCount);
        }
    }
}
