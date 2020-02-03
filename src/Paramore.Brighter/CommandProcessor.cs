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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.FeatureSwitch;
using Paramore.Brighter.Logging;
using Polly;
using Polly.Registry;

namespace Paramore.Brighter
{
    /// <summary>
    /// Class CommandProcessor.
    /// Implements both the <a href="http://www.hillside.net/plop/plop2001/accepted_submissions/PLoP2001/bdupireandebfernandez0/PLoP2001_bdupireandebfernandez0_1.pdf">Command Dispatcher</a> 
    /// and <a href="http://wiki.hsr.ch/APF/files/CommandProcessor.pdf">Command Processor</a> Design Patterns 
    /// </summary>
    public class CommandProcessor : IAmACommandProcessor, IDisposable
    {
        private static readonly Lazy<ILog> _logger = new Lazy<ILog>(LogProvider.For<CommandProcessor>);

        private readonly IAmAMessageMapperRegistry _mapperRegistry;
        private readonly IAmASubscriberRegistry _subscriberRegistry;
        private readonly IAmAHandlerFactory _handlerFactory;
        private readonly IAmAHandlerFactoryAsync _asyncHandlerFactory;
        private readonly IAmARequestContextFactory _requestContextFactory;
        private readonly IPolicyRegistry<string>  _policyRegistry;
        private readonly int _outboxTimeout;
        private readonly IAmAnOutbox<Message> _outBox;
        private readonly IAmAnOutboxAsync<Message> _asyncOutbox;
        private readonly InboxConfiguration _inboxConfiguration;
        private readonly IAmAFeatureSwitchRegistry _featureSwitchRegistry;

        // the following are not readonly to allow setting them to null on dispose
        private IAmAMessageProducer _messageProducer;
        private IAmAChannelFactory _responseChannelFactory;
        private IAmAMessageProducerAsync _asyncMessageProducer;
        private bool _disposed;

        /// <summary>
        /// Use this as an identifier for your <see cref="Policy"/> that determines for how long to break the circuit when communication with the Work Queue fails.
        /// Register that policy with your <see cref="IPolicyRegistry{TKey}"/> such as <see cref="PolicyRegistry"/>
        /// You can use this an identifier for you own policies, if your generic policy is the same as your Work Queue policy.
        /// </summary>
        public const string CIRCUITBREAKER = "Paramore.Brighter.CommandProcessor.CircuitBreaker";
        /// <summary>
        /// Use this as an identifier for your <see cref="Policy"/> that determines the retry strategy when communication with the Work Queue fails.
        /// Register that policy with your <see cref="IAmAPolicyRegistry"/> such as <see cref="PolicyRegistry"/>
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
        /// Initializes a new instance of the <see cref="CommandProcessor"/> class.
        /// Use this constructor when no task queue support is required
        /// </summary>
        /// <param name="subscriberRegistry">The subscriber registry.</param>
        /// <param name="handlerFactory">The handler factory.</param>
        /// <param name="asyncHandlerFactory">The async handler factory.</param>
        /// <param name="requestContextFactory">The request context factory.</param>
        /// <param name="policyRegistry">The policy registry.</param>
        /// <param name="featureSwitchRegistry">The feature switch config provider.</param>
        /// <param name="inboxConfiguration">Do we want to insert an inbox handler into pipelines without the attribute. Null (default = no), yes = how to configure</param>
        public CommandProcessor(
            IAmASubscriberRegistry subscriberRegistry,
            IAmAHandlerFactory handlerFactory,
            IAmAHandlerFactoryAsync asyncHandlerFactory,
            IAmARequestContextFactory requestContextFactory,
            IPolicyRegistry<string> policyRegistry,
            IAmAFeatureSwitchRegistry featureSwitchRegistry = null,
            InboxConfiguration inboxConfiguration = null)
        {
            _subscriberRegistry = subscriberRegistry;
            _handlerFactory = handlerFactory;
            _asyncHandlerFactory = asyncHandlerFactory;
            _requestContextFactory = requestContextFactory;
            _policyRegistry = policyRegistry;
            _featureSwitchRegistry = featureSwitchRegistry;
            _inboxConfiguration = inboxConfiguration;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandProcessor"/> class.
        /// Use this constructor when no task queue support is required
        /// </summary>
        /// <param name="subscriberRegistry">The subscriber registry.</param>
        /// <param name="handlerFactory">The handler factory.</param>
        /// <param name="requestContextFactory">The request context factory.</param>
        /// <param name="policyRegistry">The policy registry.</param>
        /// <param name="featureSwitchRegistry">The feature switch config provider.</param>
        /// <param name="inboxConfiguration">Do we want to insert an inbox handler into pipelines without the attribute. Null (default = no), yes = how to configure</param>
        public CommandProcessor(
            IAmASubscriberRegistry subscriberRegistry,
            IAmAHandlerFactory handlerFactory,
            IAmARequestContextFactory requestContextFactory,
            IPolicyRegistry<string>  policyRegistry,
            IAmAFeatureSwitchRegistry featureSwitchRegistry = null,
            InboxConfiguration inboxConfiguration = null
            )
        {
            _subscriberRegistry = subscriberRegistry;
            _handlerFactory = handlerFactory;
            _requestContextFactory = requestContextFactory;
            _policyRegistry = policyRegistry;
            _featureSwitchRegistry = featureSwitchRegistry;
            _inboxConfiguration = inboxConfiguration;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandProcessor"/> class.
        /// Use this constructor when no task queue support is required and only async handlers are used
        /// </summary>
        /// <param name="subscriberRegistry">The subscriber registry.</param>
        /// <param name="asyncHandlerFactory">The async handler factory.</param>
        /// <param name="requestContextFactory">The request context factory.</param>
        /// <param name="policyRegistry">The policy registry.</param>
        /// <param name="featureSwitchRegistry">The feature switch config provider.</param>
        /// <param name="inboxConfiguration">Do we want to insert an inbox handler into pipelines without the attribute. Null (default = no), yes = how to configure</param>
        public CommandProcessor(
            IAmASubscriberRegistry subscriberRegistry,
            IAmAHandlerFactoryAsync asyncHandlerFactory,
            IAmARequestContextFactory requestContextFactory,
            IPolicyRegistry<string>  policyRegistry,
            IAmAFeatureSwitchRegistry featureSwitchRegistry = null,
            InboxConfiguration inboxConfiguration = null)
        {
            _subscriberRegistry = subscriberRegistry;
            _asyncHandlerFactory = asyncHandlerFactory;
            _requestContextFactory = requestContextFactory;
            _policyRegistry = policyRegistry;
            _featureSwitchRegistry = featureSwitchRegistry;
            _inboxConfiguration = inboxConfiguration;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandProcessor"/> class.
        /// Use this constructor when only task queue support is required
        /// </summary>
        /// <param name="requestContextFactory">The request context factory.</param>
        /// <param name="policyRegistry">The policy registry.</param>
        /// <param name="mapperRegistry">The mapper registry.</param>
        /// <param name="outBox">The outbox.</param>
        /// <param name="messageProducer">The messaging gateway.</param>
        /// <param name="outboxTimeout">How long should we wait to write to the outbox</param>
        /// <param name="featureSwitchRegistry">The feature switch config provider.</param>
        /// <param name="inboxConfiguration">Do we want to insert an inbox handler into pipelines without the attribute. Null (default = no), yes = how to configure</param>
        public CommandProcessor(
            IAmARequestContextFactory requestContextFactory,
            IPolicyRegistry<string>  policyRegistry,
            IAmAMessageMapperRegistry mapperRegistry,
            IAmAnOutbox<Message> outBox,
            IAmAMessageProducer messageProducer,
            int outboxTimeout = 300,
            IAmAFeatureSwitchRegistry featureSwitchRegistry = null,
            InboxConfiguration inboxConfiguration = null)
        {
            _requestContextFactory = requestContextFactory;
            _policyRegistry = policyRegistry;
            _outboxTimeout = outboxTimeout;
            _mapperRegistry = mapperRegistry;
            _outBox = outBox;
            _messageProducer = messageProducer;
            _featureSwitchRegistry = featureSwitchRegistry;
            _inboxConfiguration = inboxConfiguration;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandProcessor"/> class.
        /// Use this constructor when only task queue support is required
        /// </summary>
        /// <param name="requestContextFactory">The request context factory.</param>
        /// <param name="policyRegistry">The policy registry.</param>
        /// <param name="mapperRegistry">The mapper registry.</param>
        /// <param name="asyncOutbox">The outbox supporting async/await.</param>
        /// <param name="asyncMessageProducer">The messaging gateway supporting async/await.</param>
        /// <param name="outboxTimeout">How long should we wait to write to the outbox</param>
        /// <param name="featureSwitchRegistry">The feature switch config provider.</param>
        /// <param name="inboxConfiguration">Do we want to insert an inbox handler into pipelines without the attribute. Null (default = no), yes = how to configure</param>
        public CommandProcessor(
            IAmARequestContextFactory requestContextFactory,
            IPolicyRegistry<string>  policyRegistry,
            IAmAMessageMapperRegistry mapperRegistry,
            IAmAnOutboxAsync<Message> asyncOutbox,
            IAmAMessageProducerAsync asyncMessageProducer,
            int outboxTimeout = 300,
            IAmAFeatureSwitchRegistry featureSwitchRegistry = null,
            InboxConfiguration inboxConfiguration = null)
        {
            _requestContextFactory = requestContextFactory;
            _policyRegistry = policyRegistry;
            _outboxTimeout = outboxTimeout;
            _mapperRegistry = mapperRegistry;
            _asyncOutbox = asyncOutbox;
            _asyncMessageProducer = asyncMessageProducer;
            _featureSwitchRegistry = featureSwitchRegistry;
            _inboxConfiguration = inboxConfiguration;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandProcessor"/> class.
        /// Use this constructor when both rpc and command processor support is required
        /// </summary>
        /// <param name="subscriberRegistry">The subscriber registry.</param>
        /// <param name="handlerFactory">The handler factory.</param>
        /// <param name="requestContextFactory">The request context factory.</param>
        /// <param name="policyRegistry">The policy registry.</param>
        /// <param name="mapperRegistry">The mapper registry.</param>
        /// <param name="messageProducer">The messaging gateway.</param>
        /// <param name="responseChannelFactory">If we are expecting a response, then we need a channel to listen on</param>
        /// <param name="outboxTimeout">How long should we wait to write to the outbox</param>
        /// <param name="featureSwitchRegistry">The feature switch config provider.</param>
        /// <param name="inboxConfiguration">Do we want to insert an inbox handler into pipelines without the attribute. Null (default = no), yes = how to configure</param>
        public CommandProcessor(
            IAmASubscriberRegistry subscriberRegistry,
            IAmAHandlerFactory handlerFactory,
            IAmARequestContextFactory requestContextFactory,
            IPolicyRegistry<string>  policyRegistry,
            IAmAMessageMapperRegistry mapperRegistry,
            IAmAMessageProducer messageProducer,
            int outboxTimeout = 300,
            IAmAFeatureSwitchRegistry featureSwitchRegistry = null,
            IAmAChannelFactory responseChannelFactory = null,
            InboxConfiguration inboxConfiguration = null)
            : this(subscriberRegistry, handlerFactory, requestContextFactory, policyRegistry)
        {
            _mapperRegistry = mapperRegistry;
            _messageProducer = messageProducer;
            _outboxTimeout = outboxTimeout;
            _featureSwitchRegistry = featureSwitchRegistry;
            _responseChannelFactory = responseChannelFactory;
            _inboxConfiguration = inboxConfiguration;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandProcessor"/> class.
        /// Use this constructor when both task queue and command processor support is required
        /// </summary>
        /// <param name="subscriberRegistry">The subscriber registry.</param>
        /// <param name="asyncHandlerFactory">The async handler factory.</param>
        /// <param name="requestContextFactory">The request context factory.</param>
        /// <param name="policyRegistry">The policy registry.</param>
        /// <param name="mapperRegistry">The mapper registry.</param>
        /// <param name="asyncOutbox">The outbox supporting async/await.</param>
        /// <param name="asyncMessageProducer">The messaging gateway supporting async/await.</param>
        /// <param name="outboxTimeout">How long should we wait to write to the outbox</param>
        /// <param name="featureSwitchRegistry">The feature switch config provider.</param>
           /// <param name="inboxConfiguration">Do we want to insert an inbox handler into pipelines without the attribute. Null (default = no), yes = how to configure</param>
        public CommandProcessor(
            IAmASubscriberRegistry subscriberRegistry,
            IAmAHandlerFactoryAsync asyncHandlerFactory,
            IAmARequestContextFactory requestContextFactory,
            IPolicyRegistry<string>  policyRegistry,
            IAmAMessageMapperRegistry mapperRegistry,
            IAmAnOutboxAsync<Message> asyncOutbox,
            IAmAMessageProducerAsync asyncMessageProducer,
            int outboxTimeout = 300,
            IAmAFeatureSwitchRegistry featureSwitchRegistry = null,
            InboxConfiguration inboxConfiguration = null)
            : this(subscriberRegistry, asyncHandlerFactory, requestContextFactory, policyRegistry, featureSwitchRegistry)
        {
            _mapperRegistry = mapperRegistry;
            _asyncOutbox = asyncOutbox;
            _asyncMessageProducer = asyncMessageProducer;
            _outboxTimeout = outboxTimeout;
            _featureSwitchRegistry = featureSwitchRegistry;
            _inboxConfiguration = inboxConfiguration;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandProcessor"/> class.
        /// Use this constructor when both task queue and command processor support is required, and you want to inject a test logger
        /// </summary>
        /// <param name="subscriberRegistry">The subscriber registry.</param>
        /// <param name="handlerFactory">The handler factory.</param>
        /// <param name="asyncHandlerFactory">The async handler factory.</param>
        /// <param name="requestContextFactory">The request context factory.</param>
        /// <param name="policyRegistry">The policy registry.</param>
        /// <param name="mapperRegistry">The mapper registry.</param>
        /// <param name="outBox">The outbox.</param>
        /// <param name="asyncOutbox">The outbox supporting async/await.</param>
        /// <param name="messageProducer">The messaging gateway.</param>
        /// <param name="asyncMessageProducer">The messaging gateway supporting async/await.</param>
        /// <param name="outboxTimeout">How long should we wait to write to the outbox</param>
        /// <param name="featureSwitchRegistry">The feature switch config provider.</param>
        /// <param name="inboxConfiguration">Do we want to insert an inbox handler into pipelines without the attribute. Null (default = no), yes = how to configure</param>
        /// <param name="responseChannelFactory">Add response channel if doing request reply</param>
        public CommandProcessor(
            IAmASubscriberRegistry subscriberRegistry,
            IAmAHandlerFactory handlerFactory,
            IAmAHandlerFactoryAsync asyncHandlerFactory,
            IAmARequestContextFactory requestContextFactory,
            IPolicyRegistry<string>  policyRegistry,
            IAmAMessageMapperRegistry mapperRegistry,
            IAmAnOutbox<Message> outBox,
            IAmAnOutboxAsync<Message> asyncOutbox,
            IAmAMessageProducer messageProducer,
            IAmAMessageProducerAsync asyncMessageProducer,
            int outboxTimeout = 300,
            IAmAFeatureSwitchRegistry featureSwitchRegistry = null,
            InboxConfiguration inboxConfiguration = null)
            : this(subscriberRegistry, handlerFactory, asyncHandlerFactory, requestContextFactory, policyRegistry, featureSwitchRegistry)
        {
            _mapperRegistry = mapperRegistry;
            _outBox = outBox;
            _asyncOutbox = asyncOutbox;
            _messageProducer = messageProducer;
            _asyncMessageProducer = asyncMessageProducer;
            _outboxTimeout = outboxTimeout;
            _inboxConfiguration = inboxConfiguration;
        }

        /// <summary>
        /// Sends the specified command. We expect only one handler. The command is handled synchronously.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="command">The command.</param>
        /// <exception cref="System.ArgumentException">
        /// </exception>
        public void Send<T>(T command) where T : class, IRequest
        {
            if (_handlerFactory == null)
                throw new InvalidOperationException("No handler factory defined.");

            var requestContext = _requestContextFactory.Create();
            requestContext.Policies = _policyRegistry;
            requestContext.FeatureSwitches = _featureSwitchRegistry;

            using (var builder = new PipelineBuilder<T>(_subscriberRegistry, _handlerFactory, _inboxConfiguration))
            {
                _logger.Value.InfoFormat("Building send pipeline for command: {0} {1}", command.GetType(), command.Id);
                var handlerChain = builder.Build(requestContext);

                AssertValidSendPipeline(command, handlerChain.Count());

                handlerChain.First().Handle(command);
            }
        }

        /// <summary>
        /// Awaitably sends the specified command.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="command">The command.</param>
        /// <param name="continueOnCapturedContext">Should we use the calling thread's synchronization context when continuing or a default thread synchronization context. Defaults to false</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        /// <returns>awaitable <see cref="Task"/>.</returns>
        public async Task SendAsync<T>(T command, bool continueOnCapturedContext = false, CancellationToken cancellationToken = default(CancellationToken)) where T : class, IRequest
        {
            if (_asyncHandlerFactory == null)
                throw new InvalidOperationException("No async handler factory defined.");

            var requestContext = _requestContextFactory.Create();
            requestContext.Policies = _policyRegistry;
            requestContext.FeatureSwitches = _featureSwitchRegistry;

            using (var builder = new PipelineBuilder<T>(_subscriberRegistry, _asyncHandlerFactory, _inboxConfiguration))
            {
                _logger.Value.InfoFormat("Building send async pipeline for command: {0} {1}", command.GetType(), command.Id);
                var handlerChain = builder.BuildAsync(requestContext, continueOnCapturedContext);

                AssertValidSendPipeline(command, handlerChain.Count());

                await handlerChain.First().HandleAsync(command, cancellationToken).ConfigureAwait(continueOnCapturedContext);
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
        public void Publish<T>(T @event) where T : class, IRequest
        {
            if (_handlerFactory == null)
                throw new InvalidOperationException("No handler factory defined.");

            var requestContext = _requestContextFactory.Create();
            requestContext.Policies = _policyRegistry;
            requestContext.FeatureSwitches = _featureSwitchRegistry;

            using (var builder = new PipelineBuilder<T>(_subscriberRegistry, _handlerFactory, _inboxConfiguration ))
            {
                _logger.Value.InfoFormat("Building send pipeline for event: {0} {1}", @event.GetType(), @event.Id);
                var handlerChain = builder.Build(requestContext);

                var handlerCount = handlerChain.Count();

                _logger.Value.InfoFormat("Found {0} pipelines for event: {1} {2}", handlerCount, @event.GetType(), @event.Id);

                var exceptions = new List<Exception>();
                foreach (var handleRequests in handlerChain)
                {
                    try
                    {
                        handleRequests.Handle(@event);
                    }
                    catch (Exception e)
                    {
                        exceptions.Add(e);
                    }
                }

                if (exceptions.Any())
                {
                    throw new AggregateException("Failed to publish to one more handlers successfully, see inner exceptions for details", exceptions);
                }
            }
        }

        /// <summary>
        /// Publishes the specified event with async/await. We expect zero or more handlers. The events are handled synchronously and concurrently
        /// Because any pipeline might throw, yet we want to execute the remaining handler chains,  we catch exceptions on any publisher
        /// instead of stopping at the first failure and then we throw an AggregateException if any of the handlers failed, 
        /// with the InnerExceptions property containing the failures.
        /// It is up the implementer of the handler that throws to take steps to make it easy to identify the handler that threw.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="event">The event.</param>
        /// <param name="continueOnCapturedContext">Should we use the calling thread's synchronization context when continuing or a default thread synchronization context. Defaults to false</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        /// <returns>awaitable <see cref="Task"/>.</returns>
        public async Task PublishAsync<T>(T @event, bool continueOnCapturedContext = false, CancellationToken cancellationToken = default(CancellationToken)) where T : class, IRequest
        {
            if (_asyncHandlerFactory == null)
                throw new InvalidOperationException("No async handler factory defined.");

            var requestContext = _requestContextFactory.Create();
            requestContext.Policies = _policyRegistry;
            requestContext.FeatureSwitches = _featureSwitchRegistry;

            using (var builder = new PipelineBuilder<T>(_subscriberRegistry, _asyncHandlerFactory, _inboxConfiguration))
            {
                _logger.Value.InfoFormat("Building send async pipeline for event: {0} {1}", @event.GetType(), @event.Id);

                var handlerChain = builder.BuildAsync(requestContext, continueOnCapturedContext);
                var handlerCount = handlerChain.Count();

                _logger.Value.InfoFormat("Found {0} async pipelines for event: {1} {2}", handlerCount, @event.GetType(), @event.Id);

                var exceptions = new List<Exception>();
                foreach (var handler in handlerChain)
                {
                    try
                    {
                        await handler.HandleAsync(@event, cancellationToken).ConfigureAwait(continueOnCapturedContext);
                    }
                    catch (Exception e)
                    {
                        exceptions.Add(e);
                    }
                }
                if (exceptions.Count > 0)
                {
                    throw new AggregateException("Failed to async publish to one more handlers successfully, see inner exceptions for details", exceptions);
                }
            }
        }

        /// <summary>
        /// Posts the specified request. The message is placed on a task queue and into a outbox for reposting in the event of failure.
        /// You will need to configure a service that reads from the task queue to process the message
        /// Paramore.Brighter.ServiceActivator provides an endpoint for use in a windows service that reads from a queue
        /// and then Sends or Publishes the message to a <see cref="CommandProcessor"/> within that service. The decision to <see cref="Send{T}"/> or <see cref="Publish{T}"/> is based on the
        /// mapper. Your mapper can map to a <see cref="Message"/> with either a <see cref="T:MessageType.MT_COMMAND"/> , which results in a <see cref="Send{T}(T)"/> or a
        /// <see cref="T:MessageType.MT_EVENT"/> which results in a <see cref="Publish{T}(T)"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="request">The request.</param>
        /// <exception cref="System.ArgumentOutOfRangeException"></exception>
        public void Post<T>(T request) where T : class, IRequest
        {
            ClearOutbox(DepositPost(request));
        }

        /// <summary>
        /// Posts the specified request with async/await support. The message is placed on a task queue and into a outbox for reposting in the event of failure.
        /// You will need to configure a service that reads from the task queue to process the message
        /// Paramore.Brighter.ServiceActivator provides an endpoint for use in a windows service that reads from a queue
        /// and then Sends or Publishes the message to a <see cref="CommandProcessor"/> within that service. The decision to <see cref="Send{T}"/> or <see cref="Publish{T}"/> is based on the
        /// mapper. Your mapper can map to a <see cref="Message"/> with either a <see cref="T:MessageType.MT_COMMAND"/> , which results in a <see cref="Send{T}(T)"/> or a
        /// <see cref="T:MessageType.MT_EVENT"/> which results in a <see cref="Publish{T}(T)"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="request">The request.</param>
        /// <param name="continueOnCapturedContext">Should we use the calling thread's synchronization context when continuing or a default thread synchronization context. Defaults to false</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        /// <exception cref="System.ArgumentOutOfRangeException"></exception>
        /// <returns>awaitable <see cref="Task"/>.</returns>
        public async Task PostAsync<T>(T request, bool continueOnCapturedContext = false, CancellationToken cancellationToken = default(CancellationToken)) where T : class, IRequest
        {
            var messageId = await DepositPostAsync(request, continueOnCapturedContext, cancellationToken);
            await ClearOutboxAsync(new Guid[]{messageId}, continueOnCapturedContext, cancellationToken);
        }

        /// <summary>
        /// Adds a message into the outbox, and returns the id of the saved message.
        /// Intended for use with the Outbox pattern: http://gistlabs.com/2014/05/the-outbox/ normally you include the
        /// call to DepositPostBox within the scope of the transaction to write corresponding entity state to your
        /// database, that you want to signal via the request to downstream consumers
        /// Pass deposited Guid to <see cref="ClearOutbox"/> 
        /// </summary>
        /// <param name="request">The request to save to the outbox</param>
        /// <typeparam name="T">The type of the request</typeparam>
        /// <returns></returns>
        public Guid DepositPost<T>(T request) where T : class, IRequest
        {
            _logger.Value.InfoFormat("Save request: {0} {1}", request.GetType(), request.Id);

            if (_outBox == null)
                throw new InvalidOperationException("No outbox defined.");

            var messageMapper = _mapperRegistry.Get<T>();
            if (messageMapper == null)
                throw new ArgumentOutOfRangeException($"No message mapper registered for messages of type: {typeof(T)}");

            var message = messageMapper.MapToMessage(request);

            RetryAndBreakCircuit(() =>
            {
                _outBox.Add(message, _outboxTimeout);
            });

            return message.Id;
        }
        
        /// <summary>
        /// Adds a message into the outbox, and returns the id of the saved message.
        /// Intended for use with the Outbox pattern: http://gistlabs.com/2014/05/the-outbox/ normally you include the
        /// call to DepositPostBox within the scope of the transaction to write corresponding entity state to your
        /// database, that you want to signal via the request to downstream consumers
        /// Pass deposited Guid to <see cref="ClearOutboxAsync"/> 
        /// </summary>
        /// <param name="request">The request to save to the outbox</param>
        /// <typeparam name="T">The type of the request</typeparam>
        /// <returns></returns>
        public async Task<Guid> DepositPostAsync<T>(T request, bool continueOnCapturedContext = false, CancellationToken cancellationToken = default(CancellationToken)) where T : class, IRequest
        {
            _logger.Value.InfoFormat("Save request: {0} {1}", request.GetType(), request.Id);

            if (_asyncOutbox == null)
                throw new InvalidOperationException("No async outbox defined.");

            var messageMapper = _mapperRegistry.Get<T>();
            if (messageMapper == null)
                throw new ArgumentOutOfRangeException($"No message mapper registered for messages of type: {typeof(T)}");

            var message = messageMapper.MapToMessage(request);

            await RetryAndBreakCircuitAsync(async ct =>
            {
                await _asyncOutbox.AddAsync(message, _outboxTimeout, ct).ConfigureAwait(continueOnCapturedContext);
            }, continueOnCapturedContext, cancellationToken).ConfigureAwait(continueOnCapturedContext);

            return message.Id;
        }


        /// <summary>
        /// Flushes the message box message given by <param name="posts"> to the broker.
        /// Intended for use with the Outbox pattern: http://gistlabs.com/2014/05/the-outbox/ <see cref="DepositPostBox"/>
        /// </summary>
        /// <param name="posts">The posts to flush</param>
        public void ClearOutbox(params Guid[] posts)
        {
            if (_outBox == null)
                throw new InvalidOperationException("No outbox defined.");
            if (_messageProducer == null)
                throw new InvalidOperationException("No message producer defined.");


            foreach (var messageId in posts)
            {
                var message = _outBox.Get(messageId);
                if (message == null)
                    throw new NullReferenceException($"Message with Id {messageId} not found in the Outbox");
                
                _logger.Value.InfoFormat("Decoupled invocation of message: Topic:{0} Id:{1}", message.Header.Topic, messageId.ToString());

                RetryAndBreakCircuit(() => { _messageProducer.Send(message); });
                Retry(() => _outBox.MarkDispatched(messageId, DateTime.UtcNow));
            }

        }

        /// <summary>
        /// Flushes the message box message given by <param name="posts"> to the broker.
        /// Intended for use with the Outbox pattern: http://gistlabs.com/2014/05/the-outbox/ <see cref="DepositPostBoxAsync"/>
        /// </summary>
        /// <param name="posts">The posts to flush</param>
        public async Task ClearOutboxAsync(IEnumerable<Guid> posts, bool continueOnCapturedContext = false, CancellationToken cancellationToken = default(CancellationToken))
        {

            if (_asyncOutbox == null)
                throw new InvalidOperationException("No async outbox defined.");
            if (_asyncMessageProducer == null)
                throw new InvalidOperationException("No async message producer defined.");

            foreach (var messageId in posts)
            {
                var message = await _asyncOutbox.GetAsync(messageId, _outboxTimeout, cancellationToken);
                if (message == null)
                    throw new NullReferenceException($"Message with Id {messageId} not found in the Outbox");
                
                _logger.Value.InfoFormat("Decoupled invocation of message: Topic:{0} Id:{1}", message.Header.Topic, messageId.ToString());
             
                await RetryAndBreakCircuitAsync(
                    async ct => await _asyncMessageProducer.SendAsync(message).ConfigureAwait(continueOnCapturedContext), 
                    continueOnCapturedContext, cancellationToken).ConfigureAwait(continueOnCapturedContext);

                await RetryAsync(async ct => await _asyncOutbox.MarkDispatchedAsync(messageId, DateTime.UtcNow));
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
        /// <param name="timeOutInMilliseconds">The call blocks, so we must time out</param>
        /// <exception cref="NotImplementedException"></exception>
        public TResponse Call<T, TResponse>(T request, int timeOutInMilliseconds)
            where T : class, ICall where TResponse : class, IResponse
        {
            if (timeOutInMilliseconds <= 0)
            {
                throw new InvalidOperationException("Timeout to a call method must have a duration greater than zero");
            }

            var outMessageMapper = _mapperRegistry.Get<T>();
            if (outMessageMapper == null)
                throw new ArgumentOutOfRangeException(
                    $"No message mapper registered for messages of type: {typeof(T)}");
            
            var inMessageMapper = _mapperRegistry.Get<TResponse>();
            if (inMessageMapper == null)
                throw new ArgumentOutOfRangeException(
                    $"No message mapper registered for messages of type: {typeof(T)}");

            //create a reply queue via creating a consumer - we use random identifiers as we will destroy
            var channelName = Guid.NewGuid();
            var routingKey = channelName.ToString();
            using (var responseChannel =
                _responseChannelFactory.CreateChannel(
                    new Connection(
                        typeof(TResponse),
                        channelName: new ChannelName(channelName.ToString()), 
                        routingKey: new RoutingKey(routingKey))))
            {

                _logger.Value.InfoFormat("Create reply queue for topic {0}", routingKey);
                request.ReplyAddress.Topic = routingKey;
                request.ReplyAddress.CorrelationId = channelName; 
                
                //we do this to create the channel on the broker, or we won't have anything to send to; we 
                //retry in case the connection is poor. An alternative would be to extract the code from
                //the channel to create the connection, but this does not do much on a new queue
                Retry(() => responseChannel.Purge());

                var outMessage = outMessageMapper.MapToMessage(request);

                //We don't store the message, if we continue to fail further retry is left to the sender 
                 _logger.Value.DebugFormat("Sending request  with routingkey {0}", routingKey);
                Retry(() => _messageProducer.Send(outMessage));

                Message responseMessage = null;
                
                //now we block on the receiver to try and get the message, until timeout.
                 _logger.Value.DebugFormat("Awaiting response on {0}", routingKey);
                 Retry(() => responseMessage = responseChannel.Receive(timeOutInMilliseconds));

                TResponse response = default(TResponse);
                if (responseMessage.Header.MessageType != MessageType.MT_NONE)
                {
                     _logger.Value.DebugFormat("Reply received from {0}", routingKey);
                     //map to request is map to a response, but it is a request from consumer point of view. Confusing, but...
                    response = inMessageMapper.MapToRequest(responseMessage);
                    Send(response);
                }

                 _logger.Value.InfoFormat("Deleting queue for routingkey: {0}", routingKey);
                
                return response;
                
            } //clean up everything at this point, whatever happens

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

            if (disposing)
            {
                _messageProducer?.Dispose();
                _asyncMessageProducer?.Dispose();
            }

            _messageProducer = null;
            _asyncMessageProducer = null;

            _disposed = true;
        }

        private void AssertValidSendPipeline<T>(T command, int handlerCount) where T : class, IRequest
        {
            _logger.Value.InfoFormat("Found {0} pipelines for command: {1} {2}", handlerCount, typeof(T), command.Id);

            if (handlerCount > 1)
                throw new ArgumentException($"More than one handler was found for the typeof command {typeof(T)} - a command should only have one handler.");
            if (handlerCount == 0)
                throw new ArgumentException($"No command handler was found for the typeof command {typeof(T)} - a command should have exactly one handler.");
        }

        private void CheckCircuit(Action send)
        {
            _policyRegistry.Get<Policy>(CIRCUITBREAKER).Execute(send);
        }

        private async Task CheckCircuitAsync(Func<CancellationToken, Task> send, bool continueOnCapturedContext = false, CancellationToken cancellationToken = default(CancellationToken))
        {
            await _policyRegistry.Get<AsyncPolicy>(CIRCUITBREAKERASYNC)
                .ExecuteAsync(send, cancellationToken, continueOnCapturedContext)
                .ConfigureAwait(continueOnCapturedContext);
        }

        private void Retry(Action send)
        {
            _policyRegistry.Get<Policy>(RETRYPOLICY).Execute(send);
        }

        private async Task RetryAsync(Func<CancellationToken, Task> send, bool continueOnCapturedContext = false, CancellationToken cancellationToken = default(CancellationToken))
        {
            await _policyRegistry.Get<AsyncPolicy>(RETRYPOLICYASYNC)
                .ExecuteAsync(send, cancellationToken, continueOnCapturedContext)
                .ConfigureAwait(continueOnCapturedContext);
        }

         private void RetryAndBreakCircuit(Action send)
        {
            CheckCircuit(() => Retry(send));
        }

        private async Task RetryAndBreakCircuitAsync(Func<CancellationToken, Task> send, bool continueOnCapturedContext = false, CancellationToken cancellationToken = default(CancellationToken))
        {
            await CheckCircuitAsync(ct => RetryAsync(send, continueOnCapturedContext, ct), continueOnCapturedContext, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext);
        }

  }
}
