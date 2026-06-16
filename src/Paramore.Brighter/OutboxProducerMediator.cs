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
using Microsoft.Extensions.Logging.Abstractions;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Scheduler.Events;
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
        private readonly ILogger _logger;

        private readonly ResiliencePipelineRegistry<string> _resiliencePipelineRegistry;
        private readonly IAmAMessageMapperRegistry _messageMapperRegistry;
        private readonly IAmAMessageTransformerFactory _messageTransformerFactory;
        private readonly IAmAMessageTransformerFactoryAsync _messageTransformerFactoryAsync;
        private readonly bool _ownsRegistry;
        private readonly bool _ownsTransformerFactories;
        private readonly TransformPipelineBuilder _transformPipelineBuilder;
        private readonly TransformPipelineBuilderAsync _transformPipelineBuilderAsync;
        private readonly IAmAnOutboxSync<TMessage, TTransaction>? _outBox;
        private readonly IAmAnOutboxAsync<TMessage, TTransaction>? _asyncOutbox;
        private readonly int _outboxTimeout;
        private readonly IAmAProducerRegistry _producerRegistry;
        private readonly InstrumentationOptions _instrumentationOptions;
        private readonly IAmAPublicationFinder _publicationFinder;
        private readonly IAmAnOutboxCircuitBreaker? _outboxCircuitBreaker;
        private readonly ConcurrentDictionary<string, List<TMessage>> _outboxBatches = new();

        private readonly SemaphoreSlim _backgroundClearSemaphore = new(1, 1);

        //Used to checking the limit on outstanding messages for an Outbox. We throw at that point. Writes to the static
        //bool should be made thread-safe by locking the object
        private static readonly SemaphoreSlim s_checkOutstandingSemaphoreToken = new(1, 1);

        private DateTimeOffset _lastOutStandingMessageCheckAt;

        private const string NoSyncOutboxError = "A sync Outbox must be defined.";
        private const string NoAsyncOutboxError = "An async Outbox must be defined.";
            
        private int _outStandingCount;
        //an int rather than a bool so Dispose can claim it with a single atomic Interlocked.Exchange:
        //an owner and the container disposing concurrently must run CloseAll() (broker I/O) and the factory
        //disposals exactly once
        private int _disposed;
        private readonly int _maxOutStandingMessages;
        private readonly TimeSpan _maxOutStandingCheckInterval;
        private readonly Dictionary<string, object> _outBoxBag;
        private readonly IAmABrighterTracer? _tracer;
        private readonly TimeProvider _timeProvider;
        
        /// <inheritdoc />
        public IAmAnOutbox? Outbox => (IAmAnOutbox?)_outBox ?? _asyncOutbox;
        
        /// <summary>
        /// Creates an instance of the Outbox Producer Mediator
        /// </summary>
        /// <param name="producerRegistry">A registry of producers</param>
        /// <param name="resiliencePipelineRegistry">A registry for reliability policies</param>
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
        /// <param name="ownsRegistry">
        /// Does this mediator own the message mapper registry, so that <see cref="Dispose()"/> should dispose it?
        /// Defaults to <c>false</c> for the manual-wiring path, where the registry is routinely shared with a
        /// Dispatcher or another bus and must not be torn down from under it. The DI path
        /// (<c>BuildOutBoxProducerMediator</c>) news up a registry solely for this mediator and passes <c>true</c>.
        /// </param>
        /// <param name="ownsTransformerFactories">
        /// Does this mediator own the transform factories, so that <see cref="Dispose()"/> should dispose them?
        /// Defaults to <c>false</c> for the manual-wiring path; the DI path passes <c>true</c>.
        /// </param>
        /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> used to create loggers.</param>
        public OutboxProducerMediator(
            IAmAProducerRegistry producerRegistry,
            ResiliencePipelineRegistry<string> resiliencePipelineRegistry,
            IAmAMessageMapperRegistry mapperRegistry,
            IAmAMessageTransformerFactory messageTransformerFactory,
            IAmAMessageTransformerFactoryAsync messageTransformerFactoryAsync,
            IAmABrighterTracer? tracer,
            IAmAPublicationFinder publicationFinder,
            ILoggerFactory loggerFactory,
            IAmAnOutbox? outbox = null,
            IAmAnOutboxCircuitBreaker? outboxCircuitBreaker = null,
            IAmARequestContextFactory? requestContextFactory = null,
            int outboxTimeout = 300,
            int maxOutStandingMessages = -1,
            TimeSpan? maxOutStandingCheckInterval = null,
            Dictionary<string, object>? outBoxBag = null,
            TimeProvider? timeProvider = null,
            InstrumentationOptions instrumentationOptions = InstrumentationOptions.All,
            bool ownsRegistry = false,
            bool ownsTransformerFactories = false)
        {
            _logger = loggerFactory.CreateLogger<CommandProcessor>();

            _producerRegistry = producerRegistry ??
                                throw new ConfigurationException("Missing Producer Registry for External Bus Services");
            _resiliencePipelineRegistry = resiliencePipelineRegistry ??
                              throw new ConfigurationException("Missing Resilience Pipeline Registry for External Bus Services");

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

            _messageMapperRegistry = mapperRegistry;
            _messageTransformerFactory = messageTransformerFactory;
            _messageTransformerFactoryAsync = messageTransformerFactoryAsync;
            _ownsRegistry = ownsRegistry;
            _ownsTransformerFactories = ownsTransformerFactories;

            _transformPipelineBuilder = new TransformPipelineBuilder(mapperRegistry, messageTransformerFactory, instrumentationOptions, loggerFactory);
            _transformPipelineBuilderAsync =
                new TransformPipelineBuilderAsync(mapperRegistryAsync, messageTransformerFactoryAsync, instrumentationOptions, loggerFactory);

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
            //claim disposed up front with a single atomic exchange: each step below is a teardown backstop
            //that must run at most once. Interlocked closes the window two concurrent Dispose() callers (an
            //owner and the container) would otherwise share — both reading false and both re-running CloseAll()
            //broker I/O plus the factory disposals. A throw from any step must not leave the flag unclaimed.
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            if (disposing)
            {
                //guard every step independently: a failure in one must not skip the rest. Otherwise a throw
                //from CloseAll() would leak the per-resolution IServiceScope each factory retains for a
                //mapper or transform obtained but not released — the exact retention this owner exists to drain.
                try { _producerRegistry.CloseAll(); }
                catch (Exception e) { Log.FailedToCloseProducers(_logger, e); }

                //dispose only what this mediator owns. On the DI path it is the sole owner of the runtime
                //mapper/transform factories (built for it in ServiceCollectionExtensions and never registered in
                //the container), so it is constructed owning them: disposing the registry cascades to the two
                //mapper factories it holds; the two transform factories are disposed directly. Without this the
                //per-resolution IServiceScope each factory retains for a mapper or transform obtained but not
                //released is held until the process exits, not at container teardown. On the manual-wiring path
                //the registry is routinely shared with a Dispatcher or another bus, so the mediator is
                //constructed not owning it and leaves it intact for the other owner.
                if (_ownsRegistry)
                    DisposeQuietly(_messageMapperRegistry);

                if (_ownsTransformerFactories)
                {
                    DisposeQuietly(_messageTransformerFactory);
                    DisposeQuietly(_messageTransformerFactoryAsync);
                }
            }
        }

        //Disposes a member if it is IDisposable, swallowing and logging any failure so one factory's fault
        //cannot skip the remaining disposals in the teardown chain.
        private static void DisposeQuietly(object? member)
        {
            try { (member as IDisposable)?.Dispose(); }
            catch (Exception e) { Log.FailedToDisposeOwnedResource(_logger, member?.GetType().Name ?? "null", e); }
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
                GetBatchOrThrow(batchId).Add(message);
                return;
            }

            CheckOutboxOutstandingLimit();

            BrighterTracer.WriteOutboxEvent(BoxDbOperation.Add, message, requestContext.Span,
                overridingTransactionProvider != null, true, _instrumentationOptions);

            var written = await ExecuteWithResiliencePipelineAsync(
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
                GetBatchOrThrow(batchId).Add(message);
                return;
            }

            CheckOutboxOutstandingLimit();

            BrighterTracer.WriteOutboxEvent(BoxDbOperation.Add, message, requestContext.Span,
                overridingTransactionProvider != null, false, _instrumentationOptions);

            var written = ExecuteWithResiliencePipeline(() =>
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
                ExecuteWithResiliencePipeline(
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
        /// <exception cref="InvalidOperationException">Thrown if there is no outbox…8631 tokens truncated…            RequestContext requestContext,
            bool continueOnCapturedContext,
            CancellationToken cancellationToken)
        {
            var parentSpan = requestContext.Span;
            var producerSpans = new ConcurrentDictionary<string, Activity>();

            //Chunk into Topics
            try
            {
                if (_asyncOutbox is null) throw new ArgumentException(NoAsyncOutboxError);
                // Group by (wire topic, producer-lookup topic) so a batch is guaranteed to
                // resolve to a single producer — messages with the same wire topic but
                // different ProducerTopic bag values land in separate batches.
                var messagesByTopic = posts.GroupBy(m => (WireTopic: m.Header.Topic, LookupTopic: GetProducerLookupTopic(m)));

                foreach (var topicBatch in messagesByTopic)
                {
                    var producer = _producerRegistry.LookupBy(topicBatch.Key.LookupTopic);
                    var span = _tracer?.CreateProducerSpan(producer.Publication, null, requestContext.Span,
                        _instrumentationOptions);

                    if (span is not null)
                    {
                        producer.Span = span;
                        // Key is only used for uniqueness until EndSpans runs; a Uuid avoids
                        // any risk of collision from composing topic strings.
                        producerSpans.TryAdd(Uuid.NewAsString(), span);
                    }

                    if (producer is IAmABulkMessageProducerAsync bulkMessageProducer)
                    {
                        var messages = topicBatch.ToArray();

                        Log.BulkDispatchingMessages(_logger, messages.Length, topicBatch.Key.WireTopic.Value);

                        foreach (var batch in await bulkMessageProducer.CreateBatchesAsync(messages, cancellationToken))
                        {
                            var sent = await ExecuteWithResiliencePipelineAsync(
                                    async _ => await bulkMessageProducer.SendAsync(batch, cancellationToken)
                                        .ConfigureAwait(continueOnCapturedContext),
                                    requestContext,
                                    continueOnCapturedContext,
                                    cancellationToken
                                )
                                .ConfigureAwait(continueOnCapturedContext);

                            if (producer is not ISupportPublishConfirmation && sent)
                            {
                                foreach (var successfulMessage in batch.Ids())
                                {
                                    await ExecuteWithResiliencePipelineAsync(async _ =>
                                            await _asyncOutbox.MarkDispatchedAsync(
                                                successfulMessage, requestContext, _timeProvider.GetUtcNow(),
                                                cancellationToken: cancellationToken
                                            ),
                                        requestContext,
                                        cancellationToken: cancellationToken
                                    );
                                }
                            }

                            if (!sent)
                            {
                                TripTopic(batch.RoutingKey);
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
                    // Log the wire topic (Header.Topic) — where the message is going. Producer
                    // lookup uses GetProducerLookupTopic, which may differ from Header.Topic when
                    // a mapper overrode it (e.g. Reply messages routed to a dynamic reply address).
                    Log.DecoupledInvocationOfMessage(_logger, message.Header.Topic.Value, message.Id.Value);

                    var producer = _producerRegistry.LookupBy(GetProducerLookupTopic(message), message.Header.Type, requestContext);
                    var span = _tracer?.CreateProducerSpan(producer.Publication, message, parentSpan,
                        _instrumentationOptions);
                    producer.Span = span;
                    if (span != null) producerSpans.TryAdd(message.Id.Value, span);

                    if (producer is IAmAMessageProducerAsync producerAsync)
                    {
                        var sent = await ExecuteWithResiliencePipelineAsync(
                                async _ => await producerAsync.SendAsync(message, cancellationToken)
                                    .ConfigureAwait(continueOnCapturedContext),
                                requestContext,
                                continueOnCapturedContext,
                                cancellationToken
                            )
                            .ConfigureAwait(continueOnCapturedContext);

                        if (producer is not ISupportPublishConfirmation && sent)
                        {
                            await ExecuteWithResiliencePipelineAsync(
                                async _ => await _asyncOutbox.MarkDispatchedAsync(
                                    message.Id, requestContext, _timeProvider.GetUtcNow(),
                                    cancellationToken: cancellationToken
                                ),
                                requestContext,
                                cancellationToken: cancellationToken
                            );
                        }

                        if(!sent) TripTopic(message.Header.Topic);
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
                var pipeline = _transformPipelineBuilder.BuildWrapPipeline<TRequest>();
                try
                {
                    message = pipeline.Wrap(request, requestContext, publication);
                }
                finally
                {
                    //Release the pipeline after the message is built. A throwing mapper/transform release
                    //must not abort a send whose message has already been produced, so it is logged, not
                    //surfaced to the caller.
                    ReleasePipeline(pipeline, request.Id);
                }
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(request), "No message mapper defined for request");
            }

            return message;
        }

        private static void ReleasePipeline(IDisposable pipeline, Id requestId)
        {
            try
            {
                pipeline.Dispose();
            }
            catch (Exception releaseException)
            {
                Log.FailedToReleasePipeline(_logger, releaseException, requestId.Value);
            }
        }

        private static async ValueTask ReleasePipelineAsync(IAsyncDisposable pipeline, Id requestId)
        {
            try
            {
                await pipeline.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception releaseException)
            {
                Log.FailedToReleasePipeline(_logger, releaseException, requestId.Value);
            }
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
                //release asynchronously: when a handler drives this from the Proactor pump the dispose
                //runs on the single-threaded pump context, so an IAsyncDisposable mapper/transform must be
                //awaited rather than blocked on
                var pipeline = _transformPipelineBuilderAsync.BuildWrapPipeline<TRequest>();
                try
                {
                    message = await pipeline.WrapAsync(request, requestContext, publication, cancellationToken);
                }
                finally
                {
                    //Release after the message is built. A throwing mapper/transform release must not abort
                    //a send whose message has already been produced, so it is logged, not surfaced.
                    await ReleasePipelineAsync(pipeline, request.Id).ConfigureAwait(false);
                }
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
            Log.BeginCountOfOutstandingMessages(_logger);
            try
            {
                if (_outBox != null)
                {
                    if (_maxOutStandingMessages >= 0)
                    {
                        _outStandingCount = _outBox
                            .GetOutstandingMessageCount(
                                _maxOutStandingCheckInterval,
                                requestContext,
                                _maxOutStandingMessages + 1,
                                args: _outBoxBag
                            );
                    }
                    else
                    {
                        _outStandingCount = _outBox
                            .GetOutstandingMessageCount(
                                _maxOutStandingCheckInterval,
                                requestContext,
                                args: _outBoxBag
                            );
                    }

                    return;
                }

                _outStandingCount = 0;
            }
            catch (Exception ex)
            {
                //if we can't talk to the outbox, swallow the exception on this thread
                Log.ErrorGettingOutstandingMessageCount(_logger, ex);
                _outStandingCount = 0;
            }
            finally
            {
                Log.CurrentOutstandingCount(_logger, _outStandingCount);
                s_checkOutstandingSemaphoreToken.Release();
            }
        }

        private bool ExecuteWithResiliencePipeline(Action action, RequestContext? requestContext)
        {
            var resiliencePipeline = _resiliencePipelineRegistry.GetPipeline(CommandProcessor.OutboxProducer);

            try
            {
                if (requestContext?.ResilienceContext != null)
                {
                    resiliencePipeline.Execute(_ => action, requestContext.ResilienceContext);
                }
                else
                {
                    resiliencePipeline.Execute(action);
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Log.ExceptionWhilstTryingToPublishMessage(_logger, ex);
                CheckOutstandingMessages(requestContext);
                return false;
            }
        }

        private async Task<bool> ExecuteWithResiliencePipelineAsync(
            Func<CancellationToken, Task> send,
            RequestContext? requestContext,
            bool continueOnCapturedContext = true,
            CancellationToken cancellationToken = default)
        {
            var resiliencePipeline = _resiliencePipelineRegistry.GetPipeline(CommandProcessor.OutboxProducer);

            try
            {
                if (requestContext?.ResilienceContext != null)
                {
                    await resiliencePipeline
                        .ExecuteAsync(async context => await send(context.CancellationToken), requestContext.ResilienceContext)
                        .ConfigureAwait(continueOnCapturedContext);
                }
                else
                {
                    await resiliencePipeline.ExecuteAsync(async ct => await send(ct), cancellationToken)
                        .ConfigureAwait(continueOnCapturedContext);
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Log.ExceptionWhilstTryingToPublishMessage(_logger, ex);
                CheckOutstandingMessages(requestContext);
                return false;
            }
        }

        private void TripTopic(RoutingKey? routingKey)
        {
            if(!RoutingKey.IsNullOrEmpty(routingKey))
                _outboxCircuitBreaker?.TripTopic(routingKey);
        }
        
        private static partial class Log
        {
            [LoggerMessage(LogLevel.Information, "Found {NumberOfMessages} to clear out of amount {AmountToClear}")]
            public static partial void FoundMessagesToClear(ILogger logger, int numberOfMessages, int amountToClear);

            [LoggerMessage(LogLevel.Warning, "Failed to release the transform pipeline for request {Id}; the message was mapped successfully and is unaffected")]
            public static partial void FailedToReleasePipeline(ILogger logger, Exception ex, string id);
            
            [LoggerMessage(LogLevel.Debug, "Time since last check is {SecondsSinceLastCheck} seconds")]
            public static partial void TimeSinceLastCheck(ILogger logger, double secondsSinceLastCheck);
            
            [LoggerMessage(LogLevel.Debug, "Check not ready to run yet")]
            public static partial void CheckNotReadyToRunYet(ILogger logger);
            
            [LoggerMessage(LogLevel.Debug, "Running outstanding message check at {MessageCheckTime} after {SecondsSinceLastCheck} seconds wait")]
            public static partial void RunningOutstandingMessageCheck(ILogger logger, DateTimeOffset messageCheckTime, double secondsSinceLastCheck);
            
            [LoggerMessage(LogLevel.Information, "Sent message: Id:{Id}")]
            public static partial void SentMessage(ILogger logger, string id);

            [LoggerMessage(LogLevel.Warning, "Publish confirmation failed for message Id:{Id} on topic {Topic}")]
            public static partial void ConfirmationFailed(ILogger logger, string id, string topic);

            [LoggerMessage(LogLevel.Warning, "Observability failed while handling a publish confirmation; confirmation handling continued")]
            public static partial void ConfirmationObservabilityFault(ILogger logger, Exception ex);

            [LoggerMessage(LogLevel.Warning, "Error handling publish confirmation for message Id:{Id} on topic {Topic}; message left un-dispatched for Sweeper retry")]
            public static partial void ConfirmationDispatchError(ILogger logger, string id, string topic, Exception ex);
            
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

            [LoggerMessage(LogLevel.Error, "Message(s) with Id(s) {MissingIds} not found in Outbox; dispatching found messages")]
            public static partial void OutboxMessagesNotFound(ILogger logger, string missingIds);
            
            [LoggerMessage(LogLevel.Debug, "Outbox outstanding message count is: {OutstandingMessageCount}")]
            public static partial void OutboxOutstandingMessageCount(ILogger logger, int outstandingMessageCount);

            [LoggerMessage(LogLevel.Warning, "Failed to close the producer registry while disposing the mediator; continuing to dispose the mapper and transform factories")]
            public static partial void FailedToCloseProducers(ILogger logger, Exception exception);

            [LoggerMessage(LogLevel.Warning, "Failed to dispose owned resource {ResourceType} while disposing the mediator; continuing with the remaining resources")]
            public static partial void FailedToDisposeOwnedResource(ILogger logger, string resourceType, Exception exception);
        }
    }
}

