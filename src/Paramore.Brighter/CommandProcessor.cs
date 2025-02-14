#region Licence

/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.BindingAttributes;
using Paramore.Brighter.FeatureSwitch;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Tasks;
using Polly;
using Polly.Registry;
using Exception = System.Exception;

namespace Paramore.Brighter
{
    /// <summary>
    /// Class CommandProcessor.
    /// Implements both the <a href="http://www.hillside.net/plop/plop2001/accepted_submissions/PLoP2001/bdupireandebfernandez0/PLoP2001_bdupireandebfernandez0_1.pdf">Command Dispatcher</a> 
    /// and <a href="http://wiki.hsr.ch/APF/files/CommandProcessor.pdf">Command Processor</a> Design Patterns 
    /// </summary>
    public class CommandProcessor : IAmACommandProcessor
    {
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<CommandProcessor>();

        private readonly IAmASubscriberRegistry? _subscriberRegistry;
        private readonly IAmAHandlerFactorySync? _handlerFactorySync;
        private readonly IAmAHandlerFactoryAsync? _handlerFactoryAsync;
        private readonly IAmARequestContextFactory _requestContextFactory;
        private readonly IPolicyRegistry<string> _policyRegistry;
        private readonly InboxConfiguration? _inboxConfiguration;
        private readonly IAmAFeatureSwitchRegistry? _featureSwitchRegistry;
        private readonly IEnumerable<Subscription>? _replySubscriptions;
        private readonly IAmABrighterTracer? _tracer;
        private readonly IAmARequestSchedulerFactory _schedulerFactory;

        //Uses -1 to indicate no outbox and will thus force a throw on a failed publish

        // the following are not readonly to allow setting them to null on dispose
        private readonly IAmAChannelFactory? _responseChannelFactory;
        private readonly InstrumentationOptions _instrumentationOptions;

        /// <summary>
        /// Use this as an identifier for your <see cref="Policy"/> that determines for how long to break the circuit when communication with the Work Queue fails.
        /// Register that policy with your <see cref="IPolicyRegistry{TKey}"/> such as <see cref="PolicyRegistry"/>
        /// You can use this an identifier for you own policies, if your generic policy is the same as your Work Queue policy.
        /// </summary>
        public const string CIRCUITBREAKER = "Paramore.Brighter.CommandProcessor.CircuitBreaker";

        /// <summary>
        /// Use this as an identifier for your <see cref="Policy"/> that determines the retry strategy when communication with the Work Queue fails.
        /// Register that policy with your <see cref="IPolicyRegistry{TKey}"/> such as <see cref="PolicyRegistry"/>
        /// You can use this an identifier for you own policies, if your generic policy is the same as your Work Queue policy.
        /// </summary>
        public const string RETRYPOLICY = "Paramore.Brighter.CommandProcessor.RetryPolicy";

        /// <summary>
        /// Use this as an identifier for your <see cref="Policy"/> that determines for how long to break the circuit when communication with the Work Queue fails.
        /// Register that policy with your <see cref="IPolicyRegistry{TKey}"/> such as <see cref="PolicyRegistry"/>
        /// You can use this an identifier for you own policies, if your generic policy is the same as your Work Queue policy.
        /// </summary>
        public const string CIRCUITBREAKERASYNC = "Paramore.Brighter.CommandProcessor.CircuitBreaker.Async";

        /// <summary>
        /// Use this as an identifier for your <see cref="Policy"/> that determines the retry strategy when communication with the Work Queue fails.
        /// Register that policy with your <see cref="IPolicyRegistry{TKey}"/> such as <see cref="PolicyRegistry"/>
        /// You can use this an identifier for you own policies, if your generic policy is the same as your Work Queue policy.
        /// </summary>
        public const string RETRYPOLICYASYNC = "Paramore.Brighter.CommandProcessor.RetryPolicy.Async";

        /// <summary>
        /// STATIC FIELDS: Use ClearServiceBus() to reset for tests!
        /// Bus: We want to hold a reference to the bus; use double lock to let us pass parameters to the constructor from the first instance
        /// MethodCache: Used to reduce the cost of reflection for bulk calls
        /// </summary>
        private static IAmAnOutboxProducerMediator? s_mediator;
        private static readonly object s_padlock = new();
        private static readonly ConcurrentDictionary<string, MethodInfo> s_boundDepositCalls = new(); 

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandProcessor"/> class
        /// NO EXTERNAL BUS: Use this constructor when no external bus is required
        /// </summary>
        /// <param name="subscriberRegistry">The subscriber registry.</param>
        /// <param name="handlerFactory">The handler factory.</param>
        /// <param name="requestContextFactory">The request context factory.</param>
        /// <param name="policyRegistry">The policy registry.</param>
        /// <param name="featureSwitchRegistry">The feature switch config provider.</param>
        /// <param name="inboxConfiguration">Do we want to insert an inbox handler into pipelines without the attribute. Null (default = no), yes = how to configure</param>
        /// <param name="tracer">What is the tracer we will use for telemetry</param>
        /// <param name="instrumentationOptions">When creating a span for <see cref="CommandProcessor"/> operations how noisy should the attributes be</param>
        /// <param name="requestSchedulerFactory">The <see cref="IAmAMessageSchedulerFactory"/>.</param>
        public CommandProcessor(
            IAmASubscriberRegistry subscriberRegistry,
            IAmAHandlerFactory handlerFactory,
            IAmARequestContextFactory requestContextFactory,
            IPolicyRegistry<string> policyRegistry,
            IAmARequestSchedulerFactory requestSchedulerFactory,
            IAmAFeatureSwitchRegistry? featureSwitchRegistry = null,
            InboxConfiguration? inboxConfiguration = null,
            IAmABrighterTracer? tracer = null,
            InstrumentationOptions instrumentationOptions = InstrumentationOptions.All)
        {
            _subscriberRegistry = subscriberRegistry;

            if (HandlerFactoryIsNotEitherIAmAHandlerFactorySyncOrAsync(handlerFactory))
                throw new ArgumentException(
                    "No HandlerFactory has been set - either an instance of IAmAHandlerFactorySync or IAmAHandlerFactoryAsync needs to be set");

            if (handlerFactory is IAmAHandlerFactorySync handlerFactorySync)
                _handlerFactorySync = handlerFactorySync;
            if (handlerFactory is IAmAHandlerFactoryAsync handlerFactoryAsync)
                _handlerFactoryAsync = handlerFactoryAsync;

            _requestContextFactory = requestContextFactory;
            _policyRegistry = policyRegistry;
            _featureSwitchRegistry = featureSwitchRegistry;
            _inboxConfiguration = inboxConfiguration;
            _tracer = tracer;
            _instrumentationOptions = instrumentationOptions;
            _schedulerFactory = requestSchedulerFactory;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandProcessor"/> class.
        /// EXTERNAL BUS AND INTERNAL BUS: Use this constructor when both external bus and command processor support is required
        /// OPTIONAL RPC: You can use this if you want to use the command processor as a client to an external bus, but also want to support RPC
        /// </summary>
        /// <param name="subscriberRegistry">The subscriber registry.</param>
        /// <param name="handlerFactory">The handler factory.</param>
        /// <param name="requestContextFactory">The request context factory.</param>
        /// <param name="policyRegistry">The policy registry.</param>
        /// <param name="bus">The external service bus that we want to send messages over</param>
        /// <param name="featureSwitchRegistry">The feature switch config provider.</param>
        /// <param name="inboxConfiguration">Do we want to insert an inbox handler into pipelines without the attribute. Null (default = no), yes = how to configure</param>
        /// <param name="replySubscriptions">The Subscriptions for creating the reply queues</param>
        /// <param name="responseChannelFactory">If we are expecting a response, then we need a channel to listen on</param>
        /// <param name="tracer">What is the tracer we will use for telemetry</param>
        /// <param name="instrumentationOptions">When creating a span for <see cref="CommandProcessor"/> operations how noisy should the attributes be</param>
        /// <param name="requestSchedulerFactory">The <see cref="IAmAMessageSchedulerFactory"/>.</param>
        public CommandProcessor(
            IAmASubscriberRegistry subscriberRegistry,
            IAmAHandlerFactory handlerFactory,
            IAmARequestContextFactory requestContextFactory,
            IPolicyRegistry<string> policyRegistry,
            IAmAnOutboxProducerMediator bus,
            IAmARequestSchedulerFactory  requestSchedulerFactory,
            IAmAFeatureSwitchRegistry? featureSwitchRegistry = null,
            InboxConfiguration? inboxConfiguration = null,
            IEnumerable<Subscription>? replySubscriptions = null,
            IAmAChannelFactory? responseChannelFactory = null,
            IAmABrighterTracer? tracer = null,
            InstrumentationOptions instrumentationOptions = InstrumentationOptions.All)
            : this(subscriberRegistry, handlerFactory, requestContextFactory, policyRegistry, requestSchedulerFactory, featureSwitchRegistry, inboxConfiguration)
        {
            _responseChannelFactory = responseChannelFactory;
            _tracer = tracer;
            _instrumentationOptions = instrumentationOptions;
            _replySubscriptions = replySubscriptions;
            _schedulerFactory = requestSchedulerFactory;
            
            InitExtServiceBus(bus); 
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandProcessor"/> class.
        /// EXTERNAL BUS, NO INTERNAL BUS: Use this constructor when only posting messages to an external bus is required
        /// </summary>
        /// <param name="requestContextFactory">The request context factory.</param>
        /// <param name="policyRegistry">The policy registry.</param>
        /// <param name="mediator">The external service bus that we want to send messages over</param>
        /// <param name="featureSwitchRegistry">The feature switch config provider.</param>
        /// <param name="inboxConfiguration">Do we want to insert an inbox handler into pipelines without the attribute. Null (default = no), yes = how to configure</param>
        /// <param name="replySubscriptions">The Subscriptions for creating the reply queues</param>
        /// <param name="tracer">What is the tracer we will use for telemetry</param>
        /// <param name="instrumentationOptions">When creating a span for <see cref="CommandProcessor"/> operations how noisy should the attributes be</param>
        /// <param name="requestSchedulerFactory">The <see cref="IAmAMessageSchedulerFactory"/>.</param>
        public CommandProcessor(
            IAmARequestContextFactory requestContextFactory,
            IPolicyRegistry<string> policyRegistry,
            IAmAnOutboxProducerMediator mediator,
            IAmARequestSchedulerFactory requestSchedulerFactory,
            IAmAFeatureSwitchRegistry? featureSwitchRegistry = null,
            InboxConfiguration? inboxConfiguration = null,
            IEnumerable<Subscription>? replySubscriptions = null,
            IAmABrighterTracer? tracer = null,
            InstrumentationOptions instrumentationOptions = InstrumentationOptions.All)
        {
            _requestContextFactory = requestContextFactory;
            _policyRegistry = policyRegistry;
            _featureSwitchRegistry = featureSwitchRegistry;
            _inboxConfiguration = inboxConfiguration;
            _replySubscriptions = replySubscriptions;
            _tracer = tracer;
            _instrumentationOptions = instrumentationOptions;
            _schedulerFactory = requestSchedulerFactory;

            InitExtServiceBus(mediator); 
        }

        /// <summary>
        /// Sends the specified command. We expect only one handler. The command is handled synchronously.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="command">The command.</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="IAmARequestContextFactory"/> </param>
        /// <exception cref="System.ArgumentException">
        /// </exception>
        public void Send<T>(T command, RequestContext? requestContext = null) where T : class, IRequest
        {
            if (_handlerFactorySync == null)
                throw new InvalidOperationException("No handler factory defined.");

            var span = _tracer?.CreateSpan(CommandProcessorSpanOperation.Send, command, requestContext?.Span, options: _instrumentationOptions);
            var context = InitRequestContext(span, requestContext);

            if (_subscriberRegistry is null)
                throw new ArgumentException("A subscriberRegistry must be configured.");

            using var builder = new PipelineBuilder<T>(_subscriberRegistry, _handlerFactorySync, _inboxConfiguration);
            try
            {
                s_logger.LogInformation("Building send pipeline for command: {CommandType} {Id}", command.GetType(),
                    command.Id);
                var handlerChain = builder.Build(context);

                AssertValidSendPipeline(command, handlerChain.Count());

                handlerChain.First().Handle(command);
            }
            catch (Exception e)
            {
                _tracer?.AddExceptionToSpan(span, [e]);
                throw;
            }
            finally
            {
                _tracer?.EndSpan(span);
            }
        }

        /// <inheritdoc />
        public string Send<TRequest>(DateTimeOffset at, TRequest command, RequestContext? requestContext = null) where TRequest : class, IRequest
        { 
            var span = _tracer?.CreateSpan(CommandProcessorSpanOperation.Scheduler, command, requestContext?.Span, options: _instrumentationOptions);
            try
            {
                var scheduler = (IAmARequestSchedulerSync)_schedulerFactory.CreateSync(this);
                return scheduler.Schedule(command, RequestSchedulerType.Send, at);
            }
            finally
            {
                _tracer?.EndSpan(span);
            }
        }

        /// <inheritdoc />
        public string Send<TRequest>(TimeSpan delay, TRequest command, RequestContext? requestContext = null) where TRequest : class, IRequest
        {
            var span = _tracer?.CreateSpan(CommandProcessorSpanOperation.Scheduler, command, requestContext?.Span, options: _instrumentationOptions);
            try
            {
                var scheduler = (IAmARequestSchedulerSync)_schedulerFactory.CreateSync(this);
                return scheduler.Schedule(command, RequestSchedulerType.Send, delay);
            }
            finally
            {
                _tracer?.EndSpan(span);
            }
        }

        /// <summary>
        /// Sends the specified command.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="command">The command.</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="IAmARequestContextFactory"/> </param>
        /// <param name="continueOnCapturedContext">Should we use the calling thread's synchronization context when continuing or a default thread synchronization context. Defaults to false</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        /// <returns>awaitable <see cref="Task"/>.</returns>
        public async Task SendAsync<T>(
            T command, 
            RequestContext? requestContext = null, 
            bool continueOnCapturedContext = true, 
            CancellationToken cancellationToken = default
        )
            where T : class, IRequest
        {
            if (_handlerFactoryAsync == null)
                throw new InvalidOperationException("No async handler factory defined.");

            var span = _tracer?.CreateSpan(CommandProcessorSpanOperation.Send, command, requestContext?.Span, options: _instrumentationOptions);
            var context = InitRequestContext(span, requestContext);
            
            if (_subscriberRegistry is null)
                throw new ArgumentException("A subscriberRegistry must be configured.");

            using var builder = new PipelineBuilder<T>(_subscriberRegistry, _handlerFactoryAsync, _inboxConfiguration);
            try
            {
                s_logger.LogInformation("Building send async pipeline for command: {CommandType} {Id}",
                    command.GetType(), command.Id);
                var handlerChain = builder.BuildAsync(context, continueOnCapturedContext);

                AssertValidSendPipeline(command, handlerChain.Count());
                
                await handlerChain.First().HandleAsync(command, cancellationToken)
                    .ConfigureAwait(continueOnCapturedContext);
            }
            catch (Exception e)
            {
                _tracer?.AddExceptionToSpan(span, [e]);
                throw;
            }
            finally
            {
                _tracer?.EndSpan(span);
            }
        }

        /// <inheritdoc />
        public async Task<string> SendAsync<TRequest>(DateTimeOffset at, TRequest command, RequestContext? requestContext = null,
            bool continueOnCapturedContext = true, CancellationToken cancellationToken = default) where TRequest : class, IRequest
        {
            var span = _tracer?.CreateSpan(CommandProcessorSpanOperation.Scheduler, command, requestContext?.Span, options: _instrumentationOptions);
            try
            {
                var scheduler = _schedulerFactory.CreateAsync(this);
                return await scheduler.ScheduleAsync(command, RequestSchedulerType.Send, at, cancellationToken);
            }
            finally
            {
                _tracer?.EndSpan(span);
            }
        }

        /// <inheritdoc />
        public async Task<string> SendAsync<TRequest>(TimeSpan delay, TRequest command, RequestContext? requestContext = null,
            bool continueOnCapturedContext = true, CancellationToken cancellationToken = default) where TRequest : class, IRequest
        {
            var span = _tracer?.CreateSpan(CommandProcessorSpanOperation.Scheduler, command, requestContext?.Span, options: _instrumentationOptions);
            try
            {
                var scheduler = _schedulerFactory.CreateAsync(this);
                return await scheduler.ScheduleAsync(command, RequestSchedulerType.Send, delay, cancellationToken);
            }
            finally
            {
                _tracer?.EndSpan(span);
            }
        }

        /// <summary>
        /// Publishes the specified event. We expect zero or more handlers. The events are handled synchronously, in turn
        /// Because any pipeline might throw, yet we want to execute the remaining handler chains,  we catch exceptions on any publisher
        /// instead of stopping at the first failure and then we throw an AggregateException if any of the handlers failed, 
        /// with the InnerExceptions property containing the failures.
        /// It is up the implementer of the handler that throws to take steps to make it easy to identify the handler that threw.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="event">The event.</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="IAmARequestContextFactory"/> </param>
        public void Publish<T>(T @event, RequestContext? requestContext = null) where T : class, IRequest
        {
            if (_handlerFactorySync == null)
                throw new InvalidOperationException("No handler factory defined.");
            
            var span = _tracer?.CreateSpan(CommandProcessorSpanOperation.Create, @event, requestContext?.Span, options: _instrumentationOptions);
            var context = InitRequestContext(span, requestContext);

            var handlerSpans = new ConcurrentDictionary<string, Activity>();
            try
            {
                if (_subscriberRegistry is null)
                    throw new ArgumentException("A subscriberRegistry must be configured.");
                
                using var builder = new PipelineBuilder<T>(_subscriberRegistry, _handlerFactorySync, _inboxConfiguration);
                s_logger.LogInformation("Building send pipeline for event: {EventType} {Id}", @event.GetType(),
                    @event.Id);
                var handlerChain = builder.Build(context);

                var handlerCount = handlerChain.Count();

                s_logger.LogInformation("Found {HandlerCount} pipelines for event: {EventType} {Id}", handlerCount,
                   @event.GetType(), @event.Id);

                var exceptions = new ConcurrentBag<Exception>();
                Parallel.ForEach(handlerChain, (handleRequests) =>
                {
                    try
                    {
                        var handlerName = handleRequests.Name.ToString();
                        handlerSpans[handlerName] = _tracer?.CreateSpan(CommandProcessorSpanOperation.Publish, @event, span, options: _instrumentationOptions)!;
                        if(handleRequests.Context is not null)
                            handleRequests.Context.Span = handlerSpans[handlerName];
                        handleRequests.Handle(@event);
                        if(handleRequests.Context is not null)
                            handleRequests.Context.Span = span;
                    }
                    catch (Exception e)
                    {
                        exceptions.Add(e);
                    }
                });
                
                _tracer?.LinkSpans(handlerSpans);

                if (exceptions.Any())
                {
                    _tracer?.AddExceptionToSpan(span, exceptions);
                    throw new AggregateException(
                        "Failed to publish to one more handlers successfully, see inner exceptions for details",
                        exceptions);
                }
            }
            finally
            {
                _tracer?.EndSpans(handlerSpans);
                _tracer?.EndSpan(span);
            }
        }

        /// <inheritdoc />
        public string Publish<TRequest>(DateTimeOffset at, TRequest @event, RequestContext? requestContext = null) where TRequest : class, IRequest
        {
            var span = _tracer?.CreateSpan(CommandProcessorSpanOperation.Scheduler, @event, requestContext?.Span, options: _instrumentationOptions);
            try
            {
                var scheduler = _schedulerFactory.CreateSync(this);
                return scheduler.Schedule(@event, RequestSchedulerType.Publish, at);
            }
            finally
            {
                _tracer?.EndSpan(span);
            }
        }

        /// <inheritdoc />
        public string  Publish<TRequest>(TimeSpan delay, TRequest @event, RequestContext? requestContext = null) where TRequest : class, IRequest
        {
            var span = _tracer?.CreateSpan(CommandProcessorSpanOperation.Scheduler, @event, requestContext?.Span, options: _instrumentationOptions);
            try
            {
                var scheduler = _schedulerFactory.CreateSync(this);
                return scheduler.Schedule(@event, RequestSchedulerType.Publish, delay);
            }
            finally
            {
                _tracer?.EndSpan(span);
            }
        }

        /// <summary>
        /// Publishes the specified event. We expect zero or more handlers. The events are handled synchronously and concurrently
        /// Because any pipeline might throw, yet we want to execute the remaining handler chains,  we catch exceptions on any publisher
        /// instead of stopping at the first failure and then we throw an AggregateException if any of the handlers failed, 
        /// with the InnerExceptions property containing the failures.
        /// It is up the implementer of the handler that throws to take steps to make it easy to identify the handler that threw.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="event">The event.</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="IAmARequestContextFactory"/> </param>
        /// <param name="continueOnCapturedContext">Should we use the calling thread's synchronization context when continuing or a default thread synchronization context. Defaults to false</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        /// <returns>awaitable <see cref="Task"/>.</returns>
        public async Task PublishAsync<T>(
            T @event,
            RequestContext? requestContext = null,
            bool continueOnCapturedContext = true,
            CancellationToken cancellationToken = default)
            where T : class, IRequest
        {
            if (_handlerFactoryAsync == null)
                throw new InvalidOperationException("No async handler factory defined.");

            var span = _tracer?.CreateSpan(CommandProcessorSpanOperation.Create, @event, requestContext?.Span, options: _instrumentationOptions);
            var context = InitRequestContext(span, requestContext);

            if (_subscriberRegistry is null)
                throw new ArgumentException("A subscriberRegistry must be configured.");
            
            using var builder = new PipelineBuilder<T>(_subscriberRegistry, _handlerFactoryAsync, _inboxConfiguration);
            var handlerSpans = new ConcurrentDictionary<string, Activity>();
             try
            {
                s_logger.LogInformation("Building send async pipeline for event: {EventType} {Id}", @event.GetType(),
                    @event.Id);

                var handlerChain = builder.BuildAsync(context, continueOnCapturedContext);
                var handlerCount = handlerChain.Count();

                s_logger.LogInformation("Found {0} async pipelines for event: {EventType} {Id}", handlerCount,
                    @event.GetType(), @event.Id
                );

                var exceptions = new ConcurrentBag<Exception>();

                try
                {
                    var tasks = new List<Task>();
                    foreach (var handleRequests in handlerChain)
                    {
                        handlerSpans[handleRequests.Name.ToString()] = _tracer?.CreateSpan(CommandProcessorSpanOperation.Publish, @event, span, options: _instrumentationOptions)!;
                        if(handleRequests.Context is not null)
                            handleRequests.Context.Span = handlerSpans[handleRequests.Name.ToString()];
                        tasks.Add(handleRequests.HandleAsync(@event, cancellationToken));
                        if(handleRequests.Context is not null)
                            handleRequests.Context.Span = span;
                    }
                    
                    await Task.WhenAll(tasks).ConfigureAwait(continueOnCapturedContext);
                }
                catch (Exception e)
                {
                    exceptions.Add(e);
                }

                _tracer?.LinkSpans(handlerSpans);

                if (exceptions.Any())
                    _tracer?.AddExceptionToSpan(span, exceptions);

                if (exceptions.Count > 0)
                {
                    throw new AggregateException(
                        "Failed to async publish to one more handlers successfully, see inner exceptions for details",
                        exceptions);
                }
            }
            finally
            {
                _tracer?.EndSpans(handlerSpans);
                _tracer?.EndSpan(span);
            }
        }

        /// <inheritdoc />
        public async Task<string> PublishAsync<TRequest>(DateTimeOffset at, TRequest @event, RequestContext? requestContext = null,
            bool continueOnCapturedContext = true, CancellationToken cancellationToken = default) where TRequest : class, IRequest
        {
            var span = _tracer?.CreateSpan(CommandProcessorSpanOperation.Scheduler, @event, requestContext?.Span, options: _instrumentationOptions);
            try
            {
                var scheduler = _schedulerFactory.CreateAsync(this);
                return await scheduler.ScheduleAsync(@event, RequestSchedulerType.Publish, at, cancellationToken);
            }
            finally
            {
                _tracer?.EndSpan(span);
            }
        }

        /// <inheritdoc />
        public async Task<string> PublishAsync<TRequest>(TimeSpan delay, TRequest @event, RequestContext? requestContext = null,
            bool continueOnCapturedContext = true, CancellationToken cancellationToken = default) where TRequest : class, IRequest
        {
            var span = _tracer?.CreateSpan(CommandProcessorSpanOperation.Scheduler, @event, requestContext?.Span, options: _instrumentationOptions);
            try
            {
                var scheduler = _schedulerFactory.CreateAsync(this);
                return await scheduler.ScheduleAsync(@event, RequestSchedulerType.Publish, delay, cancellationToken);
            }
            finally
            {
                _tracer?.EndSpan(span);
            }
        }

        /// <summary>
        /// Posts the specified request. The message is placed on a task queue and into a outbox for reposting in the event of failure.
        /// You will need to configure a service that reads from the task queue to process the message
        /// Paramore.Brighter.ServiceActivator provides an endpoint for use in a windows service that reads from a queue
        /// and then Sends or Publishes the message to a <see cref="CommandProcessor"/> within that service. The decision to <see cref="Send{T}"/> or <see cref="Publish{T}"/> is based on the
        /// mapper. Your mapper can map to a <see cref="Message"/> with either a <see cref="T:MessageType.MT_COMMAND"/> , which results in a <see cref="Send{T}"/> or a
        /// <see cref="T:MessageType.MT_EVENT"/> which results in a <see cref="Publish{T}"/>
        /// Please note that this call will not participate in any ambient Transactions, if you wish to have the outbox participate in a Transaction please Use Deposit,
        /// and then after you have committed your transaction use ClearOutstandingFromOutbox
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="IAmARequestContextFactory"/> </param>
        /// <param name="args">For transports or outboxes that require additional parameters such as topic, provide an optional arg</param>
        /// <typeparam name="TRequest">The type of request</typeparam>
        /// <exception cref="System.ArgumentOutOfRangeException"></exception>
        public void Post<TRequest>(
            TRequest request, 
            RequestContext? requestContext = null, 
            Dictionary<string, object>? args = null
        ) where TRequest : class, IRequest
        {
            ClearOutbox(new []{DepositPost(request, (IAmABoxTransactionProvider<CommittableTransaction>?)null, requestContext, args)}, requestContext, args);
        }

        /// <inheritdoc />
        public string Post<TRequest>(DateTimeOffset at, TRequest request, RequestContext? requestContext = null,
            Dictionary<string, object>? args = null) where TRequest : class, IRequest
        {
            var span = _tracer?.CreateSpan(CommandProcessorSpanOperation.Scheduler, request, requestContext?.Span, options: _instrumentationOptions);
            try
            {
                var scheduler = _schedulerFactory.CreateSync(this);
                return scheduler.Schedule(request, RequestSchedulerType.Post, at);
            }
            finally
            {
                _tracer?.EndSpan(span);
            } 
        }

        /// <inheritdoc />
        public string Post<TRequest>(TimeSpan delay, TRequest request, RequestContext? requestContext = null, Dictionary<string, object>? args = null) where TRequest : class, IRequest
        { 
            var span = _tracer?.CreateSpan(CommandProcessorSpanOperation.Scheduler, request, requestContext?.Span, options: _instrumentationOptions);
            try
            {
                var scheduler = _schedulerFactory.CreateSync(this);
                return scheduler.Schedule(request, RequestSchedulerType.Post, delay);
            }
            finally
            {
                _tracer?.EndSpan(span);
            }        
        }

        /// <summary>
        /// Posts the specified request. The message is placed on a task queue and into a outbox for reposting in the event of failure.
        /// You will need to configure a service that reads from the task queue to process the message
        /// Paramore.Brighter.ServiceActivator provides an endpoint for use in a windows service that reads from a queue
        /// and then Sends or Publishes the message to a <see cref="CommandProcessor"/> within that service. The decision to <see cref="Send{T}"/> or <see cref="Publish{T}"/> is based on the
        /// mapper. Your mapper can map to a <see cref="Message"/> with either a <see cref="T:MessageType.MT_COMMAND"/> , which results in a <see cref="Send{T}"/> or a
        /// <see cref="T:MessageType.MT_EVENT"/> which results in a <see cref="Publish{T}"/>
        /// Please note that this call will not participate in any ambient Transactions, if you wish to have the outbox participate in a Transaction please Use DepositAsync,
        /// and then after you have committed your transaction use ClearOutboxAsync
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="IAmARequestContextFactory"/> </param>
        /// <param name="args">For transports or outboxes that require additional parameters such as topic, provide an optional arg</param>
        /// <param name="continueOnCapturedContext">Should we use the calling thread's synchronization context when continuing or a default thread synchronization context. Defaults to false</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        /// <typeparam name="TRequest">The type of request</typeparam>
        /// <exception cref="System.ArgumentOutOfRangeException"></exception>
        /// <returns>awaitable <see cref="Task"/>.</returns>
        public async Task PostAsync<TRequest>(
            TRequest request,
            RequestContext? requestContext = null,
            Dictionary<string, object>? args = null,
            bool continueOnCapturedContext = true,
            CancellationToken cancellationToken = default
            )
            where TRequest : class, IRequest
        {
            var messageId = await DepositPostAsync(request, (IAmABoxTransactionProvider<CommittableTransaction>?)null, requestContext, args, continueOnCapturedContext, cancellationToken);
            await ClearOutboxAsync(new[] { messageId }, requestContext, args, continueOnCapturedContext, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<string> PostAsync<TRequest>(DateTimeOffset at, TRequest request, RequestContext? requestContext = null,
            Dictionary<string, object>? args = null, bool continueOnCapturedContext = true, CancellationToken cancellationToken = default) where TRequest : class, IRequest
        {
            var span = _tracer?.CreateSpan(CommandProcessorSpanOperation.Scheduler, request, requestContext?.Span, options: _instrumentationOptions);
            try
            {
                var scheduler = _schedulerFactory.CreateAsync(this);
                return await scheduler.ScheduleAsync(request, RequestSchedulerType.Post, at, cancellationToken);
            }
            finally
            {
                _tracer?.EndSpan(span);
            }        
        }

        /// <inheritdoc />
        public async Task<string> PostAsync<TRequest>(TimeSpan delay, TRequest request, RequestContext? requestContext = null,
            Dictionary<string, object>? args = null, bool continueOnCapturedContext = true, CancellationToken cancellationToken = default) where TRequest : class, IRequest
        {
            var span = _tracer?.CreateSpan(CommandProcessorSpanOperation.Scheduler, request, requestContext?.Span, options: _instrumentationOptions);
            try
            {
                var scheduler = _schedulerFactory.CreateAsync(this);
                return await scheduler.ScheduleAsync(request, RequestSchedulerType.Post, delay, cancellationToken);
            }
            finally
            {
                _tracer?.EndSpan(span);
            }        
        }

        /// <summary>
        /// Adds a message into the outbox, and returns the id of the saved message.
        /// Intended for use with the Outbox pattern: http://gistlabs.com/2014/05/the-outbox/ normally you include the
        /// call to DepositPostBox within the scope of the transaction to write corresponding entity state to your
        /// database, that you want to signal via the request to downstream consumers
        /// Pass deposited message to <see cref="ClearOutbox(string[],Paramore.Brighter.RequestContext,System.Collections.Generic.Dictionary{string,object})"/> 
        /// </summary>
        /// <param name="request">The request to save to the outbox</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="IAmARequestContextFactory"/> </param>
        /// <param name="args">For transports or outboxes that require additional parameters such as topic, provide an optional arg</param>
        /// <typeparam name="TRequest">The type of the request</typeparam>
        /// <returns>The Id of the Message that has been deposited.</returns>
        public string DepositPost<TRequest>(TRequest request, RequestContext? requestContext = null, Dictionary<string, object>? args = null) 
            where TRequest : class, IRequest
        {
            return DepositPost<TRequest, CommittableTransaction>(request, null, requestContext, args); 
        }

        /// <summary>
        /// Adds a message into the outbox, and returns the id of the saved message.
        /// Intended for use with the Outbox pattern: http://gistlabs.com/2014/05/the-outbox/ normally you include the
        /// call to DepositPostBox within the scope of the transaction to write corresponding entity state to your
        /// database, that you want to signal via the request to downstream consumers
        /// Pass deposited message to <see cref="ClearOutbox(string[],Paramore.Brighter.RequestContext,System.Collections.Generic.Dictionary{string,object})"/> 
        /// </summary>
        /// <param name="request">The request to save to the outbox</param>
        /// <param name="transactionProvider">The transaction provider to use with an outbox</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="IAmARequestContextFactory"/> </param>
        /// <param name="args">For transports or outboxes that require additional parameters such as topic, provide an optional arg</param>
        /// <param name="batchId">The id of any batch of deposits we are called within; this will be set by the call to DepositPost with
        /// a collection of requests and there is no need to set this yourself</param>
        /// <typeparam name="TRequest">The type of the request</typeparam>
        /// <typeparam name="TTransaction">The type of Db transaction used by the Outbox</typeparam>
        /// <returns>The Id of the Message that has been deposited.</returns>
        [DepositCallSite] //NOTE: if you adjust the signature, adjust the bulk caller
        public string DepositPost<TRequest, TTransaction>(
            TRequest request,
            IAmABoxTransactionProvider<TTransaction>? transactionProvider,
            RequestContext? requestContext = null,
            Dictionary<string, object>? args = null,
            string? batchId = null) 
            where TRequest : class, IRequest
        {
            s_logger.LogInformation("Save request: {RequestType} {Id}", request.GetType(), request.Id);
            
             var span = _tracer?.CreateSpan(CommandProcessorSpanOperation.Deposit, request, requestContext?.Span, options: _instrumentationOptions);
             var context = InitRequestContext(span, requestContext);

            try
            {
                Message message = s_mediator!.CreateMessageFromRequest(request, context);
                
                var mediator = ((IAmAnOutboxProducerMediator<Message, TTransaction>)s_mediator);

                if (!mediator.HasOutbox())
                    throw new InvalidOperationException("No outbox defined.");

                mediator.AddToOutbox(message, context, transactionProvider, batchId);

                return message.Id;
            }
            catch (Exception e)
            {
                _tracer?.AddExceptionToSpan(span, [e]);
                throw;
            }
            finally
            {
                _tracer?.EndSpan(span);
            }
        }

        /// <summary>
        /// Adds a messages into the outbox, and returns the id of the saved message.
        /// Intended for use with the Outbox pattern: http://gistlabs.com/2014/05/the-outbox/ normally you include the
        /// call to DepositPostBox within the scope of the transaction to write corresponding entity state to your
        /// database, that you want to signal via the request to downstream consumers
        /// Pass deposited message to <see cref="ClearOutbox(string[],Paramore.Brighter.RequestContext,System.Collections.Generic.Dictionary{string,object})"/> 
        /// </summary>
        /// <param name="requests">The requests to save to the outbox</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="IAmARequestContextFactory"/> </param>
        /// <param name="args">For transports or outboxes that require additional parameters such as topic, provide an optional arg</param>
        /// <typeparam name="TRequest">The type of the request</typeparam>
        /// <returns>The Id of the Message that has been deposited.</returns>
        public string[] DepositPost<TRequest>(IEnumerable<TRequest> requests, RequestContext? requestContext = null, Dictionary<string, object>? args = null) 
            where TRequest : class, IRequest
        {
            return DepositPost<TRequest, CommittableTransaction >(requests, null, requestContext, args); 
        }

        /// <summary>
        /// Adds a messages into the outbox, and returns the id of the saved message.
        /// Intended for use with the Outbox pattern: http://gistlabs.com/2014/05/the-outbox/ normally you include the
        /// call to DepositPostBox within the scope of the transaction to write corresponding entity state to your
        /// database, that you want to signal via the request to downstream consumers
        /// Pass deposited message to <see cref="ClearOutbox(string[],Paramore.Brighter.RequestContext,System.Collections.Generic.Dictionary{string,object})"/> 
        /// </summary>
        /// <param name="requests">The requests to save to the outbox</param>
        /// <param name="transactionProvider">The transaction provider to use with an outbox</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="IAmARequestContextFactory"/> </param>
        /// <param name="args">For transports or outboxes that require additional parameters such as topic, provide an optional arg</param>
        /// <typeparam name="TRequest">The type of the request</typeparam>
        /// <typeparam name="TTransaction">The type of transaction used by the Outbox</typeparam>
        /// <returns>The Id of the Message that has been deposited.</returns>
        public string[] DepositPost<TRequest, TTransaction>(
            IEnumerable<TRequest> requests,
            IAmABoxTransactionProvider<TTransaction>? transactionProvider,
            RequestContext? requestContext = null,
            Dictionary<string, object>? args = null
        ) where TRequest : class, IRequest
        {
            s_logger.LogInformation("Save bulk requests request: {RequestType}", typeof(TRequest));
            
            var span = _tracer?.CreateBatchSpan<TRequest>(requestContext?.Span, options: _instrumentationOptions);
            var context = InitRequestContext(span, requestContext);
            
            try
            {
                var successfullySentMessage = new List<string>();
                
                var mediator = (IAmAnOutboxProducerMediator<Message, TTransaction>)s_mediator!;
                
                var batchId = mediator.StartBatchAddToOutbox();

                foreach (var request in requests)
                {
                    var createSpan = context.Span;
                    var messageId = CallDepositPost(request, transactionProvider, context, args, batchId);
                    successfullySentMessage.Add(messageId);
                    context.Span = createSpan;
                }
                
                mediator.EndBatchAddToOutbox(batchId, transactionProvider, context);
                
                return successfullySentMessage.ToArray();
            }
            catch (Exception e)
            {
                _tracer?.AddExceptionToSpan(span, [e]);
                throw;
            }
            finally
            {
                _tracer?.EndSpan(span);
            }
            
            // Call the deposit post method for a single request
            // We need to bind DepositPost to the type of the request; an IEnumerable<IRequest> loses type information
            // so you need to call GetType to find the actual type. Our generic pipeline creates errors because our 
            // generic methods, like DepositPost, assume they have the derived type. This binds DepositPost to the right
            // type before we call it.
            string CallDepositPost(TRequest actualRequest, IAmABoxTransactionProvider<TTransaction>? amABoxTransactionProvider, 
                RequestContext? requestContext1, Dictionary<string, object>? dictionary, string batchId)
            {
                MethodInfo deposit;
                var actualRequestType = actualRequest.GetType();

                if (s_boundDepositCalls.ContainsKey(actualRequestType.Name))
                {
                    deposit = s_boundDepositCalls[actualRequestType.Name];
                }
                else
                {
                    var depositMethod = typeof(CommandProcessor)
                        .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                        .Where(m =>
                            m.Name == nameof(DepositPost)
                            && m.GetCustomAttributes().Any(a => a.GetType() == typeof(DepositCallSiteAttribute))
                        )
                        .FirstOrDefault(m => m.IsGenericMethod && m.GetParameters().Length == 5);

                    deposit = depositMethod?.MakeGenericMethod(actualRequestType, typeof(TTransaction))!;
                    
                    s_boundDepositCalls[actualRequestType.Name] = deposit;
                }

                return (deposit?.Invoke(this, new object?[] { actualRequest, amABoxTransactionProvider, requestContext1, dictionary, batchId }) as string)!;
            }
        }

        /// <summary>
        /// Adds a message into the outbox, and returns the id of the saved message.
        /// Intended for use with the Outbox pattern: http://gistlabs.com/2014/05/the-outbox/ normally you include the
        /// call to DepositPostBox within the scope of the transaction to write corresponding entity state to your
        /// database, that you want to signal via the request to downstream consumers
        /// Pass deposited string to <see cref="ClearOutboxAsync"/>
        /// NOTE: If you get an error about the transaction type not matching CommittableTransaction, then you need to
        /// use the specialized version of this method that takes a transaction provider.
        /// </summary>
        /// <param name="request">The request to save to the outbox</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="IAmARequestContextFactory"/> </param>
        /// <param name="args">For transports or outboxes that require additional parameters such as topic, provide an optional arg</param>
        /// <param name="continueOnCapturedContext">Should we use the calling thread's synchronization context when continuing or a default thread synchronization context. Defaults to false</param>
        /// <param name="cancellationToken">The Cancellation Token.</param>
        /// <typeparam name="TRequest">The type of the request</typeparam>
        /// <returns></returns>
        public async Task<string> DepositPostAsync<TRequest>(
            TRequest request,
            RequestContext? requestContext = null,
            Dictionary<string, object>? args = null,
            bool continueOnCapturedContext = true,
            CancellationToken cancellationToken = default) where TRequest : class, IRequest
        {
            return await DepositPostAsync<TRequest, CommittableTransaction>(
                request,
                null,
                requestContext,
                args,
                continueOnCapturedContext,
                cancellationToken);
        }

        /// <summary>
        /// Adds a message into the outbox, and returns the id of the saved message.
        /// Intended for use with the Outbox pattern: http://gistlabs.com/2014/05/the-outbox/ normally you include the
        /// call to DepositPostBox within the scope of the transaction to write corresponding entity state to your
        /// database, that you want to signal via the request to downstream consumers
        /// Pass deposited message to <see cref="ClearOutboxAsync"/> 
        /// </summary>
        /// <param name="request">The request to save to the outbox</param>
        /// <param name="transactionProvider">The transaction provider to use with an outbox</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="IAmARequestContextFactory"/> </param>
        /// <param name="args">For transports or outboxes that require additional parameters such as topic, provide an optional arg</param>
        /// <param name="continueOnCapturedContext">Should we use the calling thread's synchronization context when continuing or a default thread synchronization context. Defaults to false</param>
        /// <param name="cancellationToken">The Cancellation Token.</param>
        /// <param name="batchId">The id of the deposit batch, if this isn't set items will be added to the outbox as they come in and not as a batch</param>
        /// <typeparam name="TRequest">The type of the request</typeparam>
        /// <typeparam name="TTransaction">The type of the transaction used by the Outbox</typeparam>
        /// <returns></returns>
        [DepositCallSiteAsync] //NOTE: if you adjust the signature, adjust the bulk caller
        public async Task<string> DepositPostAsync<TRequest, TTransaction>(
            TRequest request,
            IAmABoxTransactionProvider<TTransaction>? transactionProvider,
            RequestContext? requestContext = null,
            Dictionary<string, object>? args = null,
            bool continueOnCapturedContext = true,
            CancellationToken cancellationToken = default,
            string? batchId = null) where TRequest : class, IRequest
        {
            s_logger.LogInformation("Save request: {RequestType} {Id}", request.GetType(), request.Id);
            
             var span = _tracer?.CreateSpan(CommandProcessorSpanOperation.Deposit, request, requestContext?.Span, options: _instrumentationOptions);
             var context = InitRequestContext(span, requestContext);

            try
            {
                Message message = await s_mediator!.CreateMessageFromRequestAsync(request, context, cancellationToken);
                
                var mediator = ((IAmAnOutboxProducerMediator<Message, TTransaction>)s_mediator);
                
                if (!mediator.HasAsyncOutbox())
                    throw new InvalidOperationException("No async outbox defined.");

                await mediator.AddToOutboxAsync(message, context, transactionProvider, continueOnCapturedContext,
                    cancellationToken, batchId);

                return message.Id;
            }
            catch (Exception e)
            {
                _tracer?.AddExceptionToSpan(span, [e]);
                throw;
            }
            finally
            {
                _tracer?.EndSpan(span);
            }
        }

        /// <summary>
        /// Adds a message into the outbox, and returns the id of the saved message.
        /// Intended for use with the Outbox pattern: http://gistlabs.com/2014/05/the-outbox/ normally you include the
        /// call to DepositPostBox within the scope of the transaction to write corresponding entity state to your
        /// database, that you want to signal via the request to downstream consumers
        /// Pass deposited message to <see cref="ClearOutboxAsync"/> 
        /// </summary>
        /// <param name="requests">The requests to save to the outbox</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="IAmARequestContextFactory"/> </param>
        /// <param name="args">For transports or outboxes that require additional parameters such as topic, provide an optional arg</param>
        /// <param name="continueOnCapturedContext">Should we use the calling thread's synchronization context when continuing or a default thread synchronization context. Defaults to false</param>
        /// <param name="cancellationToken">The Cancellation Token.</param>
        /// <typeparam name="TRequest">The type of the request</typeparam>
        /// <returns></returns>
        public async Task<string[]> DepositPostAsync<TRequest>(
            IEnumerable<TRequest> requests,
            RequestContext? requestContext = null,
            Dictionary<string, object>? args = null,
            bool continueOnCapturedContext = true,
            CancellationToken cancellationToken = default) where TRequest : class, IRequest
        {
            return await DepositPostAsync<TRequest, CommittableTransaction>(
                requests,
                null,
                requestContext,
                args,
                continueOnCapturedContext,
                cancellationToken);
        }

        /// <summary>
        /// Adds a message into the outbox, and returns the id of the saved message.
        /// Intended for use with the Outbox pattern: http://gistlabs.com/2014/05/the-outbox/ normally you include the
        /// call to DepositPostBox within the scope of the transaction to write corresponding entity state to your
        /// database, that you want to signal via the request to downstream consumers
        /// Pass deposited message to <see cref="ClearOutboxAsync"/> 
        /// </summary>
        /// <param name="requests">The requests to save to the outbox</param>
        /// <param name="transactionProvider">The transaction provider used with the Outbox</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="IAmARequestContextFactory"/> </param>
        /// <param name="args">For transports or outboxes that require additional parameters such as topic, provide an optional arg</param>
        /// <param name="continueOnCapturedContext">Should we use the calling thread's synchronization context when continuing or a default thread synchronization context. Defaults to false</param>
        /// <param name="cancellationToken">The Cancellation Token.</param>
        /// <typeparam name="TRequest">The type of the request</typeparam>
        /// <typeparam name="TTransaction">The type of transaction used with the Outbox</typeparam>
        /// <returns></returns>
        public async Task<string[]> DepositPostAsync<TRequest, TTransaction>(
            IEnumerable<TRequest> requests,
            IAmABoxTransactionProvider<TTransaction>? transactionProvider,
            RequestContext? requestContext = null,
            Dictionary<string, object>? args = null,
            bool continueOnCapturedContext = true,
            CancellationToken cancellationToken = default) where TRequest : class, IRequest
        {
            
            var span = _tracer?.CreateBatchSpan<TRequest>(requestContext?.Span, options: _instrumentationOptions);
            var context = InitRequestContext(span, requestContext);

            try
            {
                var successfullySentMessage = new List<string>();

                var mediator = (IAmAnOutboxProducerMediator<Message, TTransaction>)s_mediator!;
                
                var batchId = mediator.StartBatchAddToOutbox();

                foreach (var request in requests)
                {
                    var createSpan = context.Span;
                    var messageId =
                        await CallDepositPostAsync(request, transactionProvider, context, args, batchId);

                    successfullySentMessage.Add(messageId); 
                    context.Span = createSpan;
                }
                await mediator.EndBatchAddToOutboxAsync(batchId, transactionProvider, context, cancellationToken);

                return successfullySentMessage.ToArray();
            }
            catch (Exception e)
            {
                _tracer?.AddExceptionToSpan(span, [e]);
                throw;
            }
            finally
            {
                _tracer?.EndSpan(span);
            }
            
            // Call the deposit post method for a single request
            // We need to bind DepositPostAsync to the type of the request; an IEnumerable<IRequest> loses type information
            // so you need to call GetType to find the actual type. Our generic pipeline creates errors because our 
            // generic methods, like DepositPost, assume they have the derived type. This binds DepositPostAsync to the right
            // type before we call it.
            Task<string> CallDepositPostAsync(TRequest actualRequest, IAmABoxTransactionProvider<TTransaction>? tp, 
                RequestContext rc, Dictionary<string, object>? bag, string? batchId = null)
            {
                MethodInfo deposit;
                var actualRequestType = actualRequest.GetType();

                if (s_boundDepositCalls.ContainsKey(actualRequestType.Name))
                {
                    deposit = s_boundDepositCalls[actualRequestType.Name];
                }
                else
                {
                    var depositMethod = typeof(CommandProcessor)
                        .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                        .Where(m =>
                            m.Name == nameof(DepositPostAsync)
                            && m.GetCustomAttributes().Any(a => a.GetType() == typeof(DepositCallSiteAsyncAttribute))
                        )
                        .FirstOrDefault(m => m.IsGenericMethod && m.GetParameters().Length == 7);

                    deposit = depositMethod?.MakeGenericMethod(actualRequest.GetType(), typeof(TTransaction))!;
                    s_boundDepositCalls[actualRequestType.Name] = deposit;
                }

                return (Task<string>)deposit?
                    .Invoke(this, new object?[] { actualRequest, tp, rc, bag, continueOnCapturedContext, cancellationToken, batchId }
                )!;
            }
        }

        /// <summary>
        /// Flushes the messages in the id list from the Outbox.
        /// Intended for use with the Outbox pattern: http://gistlabs.com/2014/05/the-outbox/ <see cref="DepositPost{TRequest}(TRequest,Paramore.Brighter.RequestContext,System.Collections.Generic.Dictionary{string,object})"/>
        /// </summary>
        /// <param name="ids">The message ids to flush</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="IAmARequestContextFactory"/> </param>
        /// <param name="args">For transports or outboxes that require additional parameters such as topic, provide an optional arg</param>
        public void ClearOutbox(string[] ids, RequestContext? requestContext = null, Dictionary<string, object>? args = null)
        {
            var span = _tracer?.CreateClearSpan(CommandProcessorSpanOperation.Create, requestContext?.Span, options: _instrumentationOptions);
            var context = InitRequestContext(span, requestContext);
            
            try
            {
                s_mediator!.ClearOutbox(ids, context, args);
            }
            catch (Exception e)
            {
                _tracer?.AddExceptionToSpan(span, [e]);
                throw;
            }
            finally
            {
                _tracer?.EndSpan(span);
            }
        }

        /// <summary>
        /// Flushes the message box message given by <param name="posts"/> to the broker.
        /// Intended for use with the Outbox pattern: http://gistlabs.com/2014/05/the-outbox/ <see cref="DepositPostAsync{TRequest}(TRequest,Paramore.Brighter.RequestContext,System.Collections.Generic.Dictionary{string,object},bool,System.Threading.CancellationToken)"/>
        /// </summary>
        /// <param name="posts">The ids to flush</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="IAmARequestContextFactory"/> </param>
        /// <param name="args">For transports or outboxes that require additional parameters such as topic, provide an optional arg</param>
        /// <param name="continueOnCapturedContext">Should the callback run on a new thread?</param>
        /// <param name="cancellationToken">The token to cancel a running asynchronous operation</param>
        public async Task ClearOutboxAsync(
            IEnumerable<string> posts,
            RequestContext? requestContext = null,
            Dictionary<string, object>? args = null,
            bool continueOnCapturedContext = true,
            CancellationToken cancellationToken = default)
        {
            var span = _tracer?.CreateClearSpan(CommandProcessorSpanOperation.Create, requestContext?.Span, options: _instrumentationOptions);
            var context = InitRequestContext(span, requestContext);
            
            try
            {
                await s_mediator!.ClearOutboxAsync(posts, context, continueOnCapturedContext, args, cancellationToken);
            }
            catch (Exception e)
            {
                _tracer?.AddExceptionToSpan(span, [e]);
                throw;
            }
            finally
            {
                _tracer?.EndSpan(span);
            }
        }
        
        /// <summary>
        /// Uses the Request-Reply messaging approach to send a message to another server and block awaiting a reply.
        /// The message is placed into a message queue but not into the outbox.
        /// An ephemeral reply queue is created, and its name used to set the reply address for the response. We produce
        /// a queue per exchange, to simplify correlating send and receive.
        /// The response is directed to a registered handler.
        /// Because the operation blocks, there is a mandatory timeout
        /// </summary>
        /// <param name="request">What message do we want a reply to</param>
        /// <param name="requestContext">The context of the request; if null we will start one via a <see cref="IAmARequestContextFactory"/> </param>
        /// <param name="timeOut">The call blocks, so we must time out</param>
        /// <exception cref="NotImplementedException"></exception>
        public TResponse? Call<T, TResponse>(T request, RequestContext? requestContext = null, TimeSpan? timeOut = null)
            where T : class, ICall where TResponse : class, IResponse
        {
            timeOut ??= TimeSpan.FromMilliseconds(500);
            
            if (timeOut <= TimeSpan.Zero)
            {
                throw new InvalidOperationException("Timeout to a call method must have a duration greater than zero");
            }

            var subscription = _replySubscriptions?.FirstOrDefault(s => s.DataType == typeof(TResponse));

            if (subscription is null)
                throw new InvalidOperationException($"No Subscription registered fpr replies of type {typeof(T)}");
            
            if (_responseChannelFactory is null)
                throw new InvalidOperationException("No ResponseChannelFactory registered");

            //create a reply queue via creating a consumer - we use random identifiers as we will destroy
            var channelName = Guid.NewGuid();
            var routingKey = channelName.ToString();

            subscription.ChannelName = new ChannelName(channelName.ToString());
            subscription.RoutingKey = new RoutingKey(routingKey);

            using var responseChannel = _responseChannelFactory.CreateSyncChannel(subscription);
            s_logger.LogInformation("Create reply queue for topic {ChannelName}", channelName);
            request.ReplyAddress.Topic = subscription.RoutingKey;
            request.ReplyAddress.CorrelationId = channelName.ToString();

            //we do this to create the channel on the broker, or we won't have anything to send to; we 
            //retry in case the subscription is poor. An alternative would be to extract the code from
            //the channel to create the subscription, but this does not do much on a new queue
            Retry(() => responseChannel.Purge());

            var span = _tracer?.CreateClearSpan(CommandProcessorSpanOperation.Create, requestContext?.Span, options: _instrumentationOptions);
            var context = InitRequestContext(span, requestContext);

            try
            {
                var outMessage = s_mediator!.CreateMessageFromRequest(request, context);

                //We don't store the message, if we continue to fail further retry is left to the sender 
                s_logger.LogDebug("Sending request  with routingkey {ChannelName}", channelName);
                s_mediator.CallViaExternalBus<T, TResponse>(outMessage, requestContext);

                Message? responseMessage = null;

            //now we block on the receiver to try and get the message, until timeout.
            s_logger.LogDebug("Awaiting response on {ChannelName}", channelName);
            Retry(() => responseMessage = responseChannel.Receive(timeOut));
            
                if (responseMessage is not null && responseMessage.Header.MessageType != MessageType.MT_NONE)
                {
                    s_logger.LogDebug("Reply received from {ChannelName}", channelName);
                    //map to request is map to a response, but it is a request from consumer point of view. Confusing, but...
                    s_mediator.CreateRequestFromMessage(responseMessage, context, out TResponse response);
                    Send(response);

                    return response;
                }

                s_logger.LogInformation("Deleting queue for routingkey: {ChannelName}", channelName);

                return null;
            } 
            catch (Exception e)
            {
                _tracer?.AddExceptionToSpan(span, [e]);
                throw;
            }
            finally
            {
                _tracer?.EndSpan(span);
            }
        }

            /// <summary>
        /// The external service bus is a singleton as it has app lifetime to manage an Outbox.
        /// This method clears the external service bus, so that the next attempt to use it will create a fresh one
        /// It is mainly intended for testing, to allow the external service bus to be reset between tests
        /// </summary>
        public static void ClearServiceBus()
        {
            if (s_mediator != null)
            {
                lock (s_padlock)
                {
                    s_mediator.Dispose();
                    s_mediator = null;
                }
            }
            s_boundDepositCalls.Clear();
        }

        private void AssertValidSendPipeline<T>(T command, int handlerCount) where T : class, IRequest
        {
            s_logger.LogInformation("Found {HandlerCount} pipelines for command: {Type} {Id}", handlerCount, typeof(T),
                command.Id);

            if (handlerCount > 1)
                throw new ArgumentException(
                    $"More than one handler was found for the typeof command {typeof(T)} - a command should only have one handler.");
            if (handlerCount == 0)
                throw new ArgumentException(
                    $"No command handler was found for the typeof command {typeof(T)} - a command should have exactly one handler.");
        }
        
        private bool HandlerFactoryIsNotEitherIAmAHandlerFactorySyncOrAsync(IAmAHandlerFactory handlerFactory)
        {
            // If we do not have a subscriber registry and we do not have a handler factory 
            // then we're creating a control bus sender and we don't need them
            if (_subscriberRegistry is null)
                return false;

            switch (handlerFactory)
            {
                case IAmAHandlerFactorySync _:
                case IAmAHandlerFactoryAsync _:
                    return false;
                default:
                    return true;
            }
        }
 
        // Create an instance of the OutboxProducerMediator if one not already set for this app. Note that we do not support reinitialization here, so once you have
        // set a command processor for the app, you can't call init again to set them - although the properties are not read-only so overwriting is possible
        // if needed as a "get out of gaol" card.
        private static void InitExtServiceBus(IAmAnOutboxProducerMediator bus)
        {
            if (s_mediator == null)
            {
                lock (s_padlock)
                {
                    s_mediator ??= bus;
                }
            }
        }
        
        private RequestContext InitRequestContext(Activity? span, RequestContext? requestContext)
        {
            var context = requestContext ?? _requestContextFactory.Create();
            context.Span = span;
            context.Policies = _policyRegistry;
            context.FeatureSwitches = _featureSwitchRegistry;
            return context;
        }
        
 
        private void Retry(Action action)
        {
            var policy = _policyRegistry.Get<Policy>(CommandProcessor.RETRYPOLICY);
            var result = policy.ExecuteAndCapture(action);
            if (result.Outcome != OutcomeType.Successful)
            {
                if (result.FinalException != null)
                {
                    s_logger.LogError(result.FinalException, "Exception whilst trying to publish message");
                }
            }
        }
    }
}
