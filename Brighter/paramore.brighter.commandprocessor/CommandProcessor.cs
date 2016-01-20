// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor
// Author           : ian
// Created          : 07-01-2014
//
// Last Modified By : ian
// Last Modified On : 07-29-2014
// ***********************************************************************
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************

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
using System.Threading.Tasks;
using paramore.brighter.commandprocessor.Logging;
using Polly;

namespace paramore.brighter.commandprocessor
{
    /// <summary>
    /// Class CommandProcessor.
    /// Implements both the <a href="http://www.hillside.net/plop/plop2001/accepted_submissions/PLoP2001/bdupireandebfernandez0/PLoP2001_bdupireandebfernandez0_1.pdf">Command Dispatcher</a> 
    /// and <a href="http://wiki.hsr.ch/APF/files/CommandProcessor.pdf">Command Processor</a> Design Patterns 
    /// </summary>
    public class CommandProcessor : IAmACommandProcessor, IDisposable
    {
        private readonly IAmAMessageMapperRegistry _mapperRegistry;
        private readonly IAmASubscriberRegistry _subscriberRegistry;
        private readonly IAmAHandlerFactory _handlerFactory;
        private readonly IAmAnAsyncHandlerFactory _asyncHandlerFactory;
        private readonly IAmARequestContextFactory _requestContextFactory;
        private readonly IAmAPolicyRegistry _policyRegistry;
        private readonly ILog _logger;
        private readonly int _messageStoreTimeout;
        private IMessageStore<Message> _messageStore;
        private IMessageProducer _messageProducer;
        private bool _disposed;

        /// <summary>
        /// Use this as an identifier for your <see cref="Policy"/> that determines for how long to break the circuit when communication with the Work Queue fails.
        /// Register that policy with your <see cref="IAmAPolicyRegistry"/> such as <see cref="PolicyRegistry"/>
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
        /// Initializes a new instance of the <see cref="CommandProcessor"/> class.
        /// Use this constructor when no task queue support is required
        /// </summary>
        /// <param name="subscriberRegistry">The subscriber registry.</param>
        /// <param name="handlerFactory">The handler factory.</param>
        /// <param name="asyncHandlerFactory">The async handler factory.</param>
        /// <param name="requestContextFactory">The request context factory.</param>
        /// <param name="policyRegistry">The policy registry.</param>
        public CommandProcessor(
            IAmASubscriberRegistry subscriberRegistry,
            IAmAHandlerFactory handlerFactory,
            IAmAnAsyncHandlerFactory asyncHandlerFactory,
            IAmARequestContextFactory requestContextFactory,
            IAmAPolicyRegistry policyRegistry)
            : this(subscriberRegistry, handlerFactory, asyncHandlerFactory, requestContextFactory, policyRegistry, LogProvider.GetCurrentClassLogger())
        {}

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandProcessor"/> class.
        /// Use this constructor when no task queue support is required
        /// </summary>
        /// <param name="subscriberRegistry">The subscriber registry.</param>
        /// <param name="handlerFactory">The handler factory.</param>
        /// <param name="requestContextFactory">The request context factory.</param>
        /// <param name="policyRegistry">The policy registry.</param>
        public CommandProcessor(
            IAmASubscriberRegistry subscriberRegistry,
            IAmAHandlerFactory handlerFactory,
            IAmARequestContextFactory requestContextFactory,
            IAmAPolicyRegistry policyRegistry)
            : this(subscriberRegistry, handlerFactory, requestContextFactory, policyRegistry, LogProvider.GetCurrentClassLogger())
        {}

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandProcessor"/> class.
        /// Use this constructor when no task queue support is required and only async handlers are used
        /// </summary>
        /// <param name="subscriberRegistry">The subscriber registry.</param>
        /// <param name="asyncHandlerFactory">The async handler factory.</param>
        /// <param name="requestContextFactory">The request context factory.</param>
        /// <param name="policyRegistry">The policy registry.</param>
        public CommandProcessor(
            IAmASubscriberRegistry subscriberRegistry,
            IAmAnAsyncHandlerFactory asyncHandlerFactory,
            IAmARequestContextFactory requestContextFactory,
            IAmAPolicyRegistry policyRegistry)
            : this(subscriberRegistry, asyncHandlerFactory, requestContextFactory, policyRegistry, LogProvider.GetCurrentClassLogger())
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandProcessor"/> class.
        /// Use this constructor when no task queue support is required, and you want to inject a test logger
        /// </summary>
        /// <param name="subscriberRegistry">The subscriber registry.</param>
        /// <param name="handlerFactory">The handler factory.</param>
        /// <param name="requestContextFactory">The request context factory.</param>
        /// <param name="policyRegistry">The policy registry.</param>
        /// <param name="logger">The logger.</param>
        public CommandProcessor(
            IAmASubscriberRegistry subscriberRegistry,
            IAmAHandlerFactory handlerFactory,
            IAmARequestContextFactory requestContextFactory,
            IAmAPolicyRegistry policyRegistry,
            ILog logger)
        {
            _subscriberRegistry = subscriberRegistry;
            _handlerFactory = handlerFactory;
            _requestContextFactory = requestContextFactory;
            _policyRegistry = policyRegistry;
            _logger = logger;
        }


        /// <summary>
        /// Initializes a new instance of the <see cref="CommandProcessor"/> class.
        /// Use this constructor when no task queue support is required and only async handlers are used
        /// </summary>
        /// <param name="subscriberRegistry">The subscriber registry.</param>
        /// <param name="asyncHandlerFactory">The async handler factory.</param>
        /// <param name="requestContextFactory">The request context factory.</param>
        /// <param name="policyRegistry">The policy registry.</param>
        /// <param name="logger">The logger.</param>
        public CommandProcessor(
            IAmASubscriberRegistry subscriberRegistry,
            IAmAnAsyncHandlerFactory asyncHandlerFactory,
            IAmARequestContextFactory requestContextFactory,
            IAmAPolicyRegistry policyRegistry,
            ILog logger)
        {
            _subscriberRegistry = subscriberRegistry;
            _asyncHandlerFactory = asyncHandlerFactory;
            _requestContextFactory = requestContextFactory;
            _policyRegistry = policyRegistry;
            _logger = logger;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandProcessor"/> class.
        /// Use this constructor when no task queue support is required, and you want to inject a test logger
        /// </summary>
        /// <param name="subscriberRegistry">The subscriber registry.</param>
        /// <param name="handlerFactory">The handler factory.</param>
        /// <param name="asyncHandlerFactory">The async handler factory.</param>
        /// <param name="requestContextFactory">The request context factory.</param>
        /// <param name="policyRegistry">The policy registry.</param>
        /// <param name="logger">The logger.</param>
        public CommandProcessor(
            IAmASubscriberRegistry subscriberRegistry,
            IAmAHandlerFactory handlerFactory,
            IAmAnAsyncHandlerFactory asyncHandlerFactory,
            IAmARequestContextFactory requestContextFactory,
            IAmAPolicyRegistry policyRegistry,
            ILog logger)
        {
            _subscriberRegistry = subscriberRegistry;
            _handlerFactory = handlerFactory;
            _asyncHandlerFactory = asyncHandlerFactory;
            _requestContextFactory = requestContextFactory;
            _policyRegistry = policyRegistry;
            _logger = logger;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandProcessor"/> class.
        /// Use this constructor when only task queue support is required
        /// </summary>
        /// <param name="requestContextFactory">The request context factory.</param>
        /// <param name="policyRegistry">The policy registry.</param>
        /// <param name="mapperRegistry">The mapper registry.</param>
        /// <param name="messageStore">The message store.</param>
        /// <param name="messageProducer">The messaging gateway.</param>
        /// <param name="messageStoreTimeout">How long should we wait to write to the message store</param>
        public CommandProcessor(
            IAmARequestContextFactory requestContextFactory,
            IAmAPolicyRegistry policyRegistry,
            IAmAMessageMapperRegistry mapperRegistry,
            IAmAMessageStore<Message> messageStore,
            IAmAMessageProducer messageProducer,
            int messageStoreTimeout = 300
            ) : this(requestContextFactory, policyRegistry, mapperRegistry, messageStore, messageProducer, LogProvider.GetCurrentClassLogger(), messageStoreTimeout) 
        {}

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandProcessor"/> class.
        /// Use this constructor when only task queue support is required, and you wish to inject a test logger
        /// </summary>
        /// <param name="requestContextFactory">The request context factory.</param>
        /// <param name="policyRegistry">The policy registry.</param>
        /// <param name="mapperRegistry">The mapper registry.</param>
        /// <param name="messageStore">The message store.</param>
        /// <param name="messageProducer">The messaging gateway.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="messageStoreTimeout">How long should we wait to write to the message store</param>
        public CommandProcessor(
            IAmARequestContextFactory requestContextFactory,
            IAmAPolicyRegistry policyRegistry,
            IAmAMessageMapperRegistry mapperRegistry,
            IMessageStore<Message> messageStore,
            IMessageProducer messageProducer,
            ILog logger,
            int messageStoreTimeout = 300
            )
        {
            _requestContextFactory = requestContextFactory;
            _policyRegistry = policyRegistry;
            _logger = logger;
            _messageStoreTimeout = messageStoreTimeout;
            _mapperRegistry = mapperRegistry;
            if (!(messageStore is IAmAMessageStore<Message> || messageStore is IAmAnAsyncMessageStore<Message>))
                throw new ArgumentException("MessageStore needs to implement IAmAMessageStore, IAmAnAsyncMessageStore or both.");
            _messageStore = messageStore;
            if (!(messageProducer is IAmAMessageProducer || messageProducer is IAmAMessageProducer))
                throw new ArgumentException("MessageProducer needs to implement IAmAMessageProducer, IAmAMessageProducer or both.");
            _messageProducer = messageProducer;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandProcessor"/> class.
        /// Use this constructor when both task queue and command processor support is required
        /// </summary>
        /// <param name="subscriberRegistry">The subscriber registry.</param>
        /// <param name="handlerFactory">The handler factory.</param>
        /// <param name="requestContextFactory">The request context factory.</param>
        /// <param name="policyRegistry">The policy registry.</param>
        /// <param name="mapperRegistry">The mapper registry.</param>
        /// <param name="messageStore">The message store.</param>
        /// <param name="messageProducer">The messaging gateway.</param>
        /// <param name="messageStoreTimeout">How long should we wait to write to the message store</param>
        /// <param name="messageGatewaySendTimeout">How long should we wait to post to the message store</param>
        public CommandProcessor(
            IAmASubscriberRegistry subscriberRegistry,
            IAmAHandlerFactory handlerFactory,
            IAmARequestContextFactory requestContextFactory,
            IAmAPolicyRegistry policyRegistry,
            IAmAMessageMapperRegistry mapperRegistry,
            IAmAMessageStore<Message> messageStore,
            IAmAMessageProducer messageProducer,
            int messageStoreTimeout = 300,
            int messageGatewaySendTimeout = 300
            )
            : this(subscriberRegistry, handlerFactory, requestContextFactory, policyRegistry, mapperRegistry, messageStore, messageProducer, LogProvider.GetCurrentClassLogger()) {}


        /// <summary>
        /// Initializes a new instance of the <see cref="CommandProcessor"/> class.
        /// Use this constructor when both task queue and command processor support is required, and you want to inject a test logger
        /// </summary>
        /// <param name="subscriberRegistry">The subscriber registry.</param>
        /// <param name="handlerFactory">The handler factory.</param>
        /// <param name="requestContextFactory">The request context factory.</param>
        /// <param name="policyRegistry">The policy registry.</param>
        /// <param name="mapperRegistry">The mapper registry.</param>
        /// <param name="messageStore">The message store.</param>
        /// <param name="messageProducer">The messaging gateway.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="messageStoreTimeout">How long should we wait to write to the message store</param>
        /// <param name="messageGatewaySendTimeout">How long should we wait to post to the message store</param>
        public CommandProcessor(
            IAmASubscriberRegistry subscriberRegistry,
            IAmAHandlerFactory handlerFactory,
            IAmARequestContextFactory requestContextFactory,
            IAmAPolicyRegistry policyRegistry,
            IAmAMessageMapperRegistry mapperRegistry,
            IMessageStore<Message> messageStore,
            IMessageProducer messageProducer,
            ILog logger,
            int messageStoreTimeout = 300,
            int messageGatewaySendTimeout = 300
            )
            : this(subscriberRegistry, handlerFactory, requestContextFactory, policyRegistry, logger)
        {
            _mapperRegistry = mapperRegistry;
            if (!(messageStore is IAmAMessageStore<Message> || messageStore is IAmAnAsyncMessageStore<Message>))
                throw new ArgumentException("MessageStore needs to implement IAmAMessageStore, IAmAnAsyncMessageStore or both.");
            _messageStore = messageStore;
            if (!(messageProducer is IAmAMessageProducer || messageProducer is IAmAMessageProducer))
                throw new ArgumentException("MessageProducer needs to implement IAmAMessageProducer, IAmAMessageProducer or both.");
            _messageProducer = messageProducer;
            _messageStoreTimeout = messageStoreTimeout;
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

            using (var builder = new PipelineBuilder<T>(_subscriberRegistry, _handlerFactory, _logger))
            {
                _logger.InfoFormat("Building send pipeline for command: {0}", command.Id);
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
        /// <returns>awaitable <see cref="Task"/>.</returns>
        public async Task SendAsync<T>(T command, bool continueOnCapturedContext = false) where T : class, IRequest
        {
            if (_asyncHandlerFactory == null)
                throw new InvalidOperationException("No async handler factory defined.");

            var requestContext = _requestContextFactory.Create();
            requestContext.Policies = _policyRegistry;

            using (var builder = new PipelineBuilder<T>(_subscriberRegistry, _asyncHandlerFactory, _logger))
            {
                _logger.InfoFormat("Building send async pipeline for command: {0}", command.Id);
                var handlerChain = builder.BuildAsync(requestContext, continueOnCapturedContext);

                AssertValidSendPipeline(command, handlerChain.Count());

                await handlerChain.First().HandleAsync(command).ConfigureAwait(continueOnCapturedContext:false);
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

            using (var builder = new PipelineBuilder<T>(_subscriberRegistry, _handlerFactory, _logger))
            {
                _logger.InfoFormat("Building send pipeline for event: {0}", @event.Id);
                var handlerChain = builder.Build(requestContext);

                var handlerCount = handlerChain.Count();

                _logger.InfoFormat("Found {0} pipelines for event: {1}", handlerCount, @event.Id);

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
        /// <returns>awaitable <see cref="Task"/>.</returns>
        public async Task PublishAsync<T>(T @event, bool continueOnCapturedContext = false) where T : class, IRequest
        {
            if (_asyncHandlerFactory == null)
                throw new InvalidOperationException("No async handler factory defined.");

            var requestContext = _requestContextFactory.Create();
            requestContext.Policies = _policyRegistry;

            using (var builder = new PipelineBuilder<T>(_subscriberRegistry, _asyncHandlerFactory, _logger))
            {
                _logger.InfoFormat("Building send async pipeline for event: {0}", @event.Id);
                var handlerChain = builder.BuildAsync(requestContext, continueOnCapturedContext);

                var handlerCount = handlerChain.Count();

                _logger.InfoFormat("Found {0} async pipelines for event: {1}", handlerCount, @event.Id);

                var eventTasks = handlerChain.Select(handleRequests => handleRequests.HandleAsync(@event)).Cast<Task>().ToList();
                // Whenall will aggregate individual exceptions, and await will raise it when all tasks have completed
                try
                {
                    await Task.WhenAll(eventTasks).ConfigureAwait(continueOnCapturedContext:false);
                }
                catch (Exception)
                {
                    var exceptions = new List<Exception>();
                    foreach (var task in eventTasks.Where(task => task.IsFaulted))
                    {
                        if (task.Exception != null) exceptions.AddRange(task.Exception.InnerExceptions);
                        else exceptions.Add(task.Exception);
                    }
                    throw new AggregateException("Failed to async publish to one more handlers successfully, see inner exceptions for details", exceptions);
                }
            }
        }

        /// <summary>
        /// Posts the specified request. The message is placed on a task queue and into a message store for reposting in the event of failure.
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
            _logger.InfoFormat("Decoupled invocation of request: {0}", request.Id);
            var theMessageStore = _messageStore as IAmAMessageStore<Message>;
            if (theMessageStore == null)
                throw new ArgumentException("MessageStore does not implement IAmAMessageStore");
            var theMessageProducer = _messageProducer as IAmAMessageProducer;
            if (theMessageProducer == null)
                throw new ArgumentException("MessageProducer does not implement IAmAMessageProducer");

            var messageMapper = _mapperRegistry.Get<T>();
            if (messageMapper == null)
                throw new ArgumentOutOfRangeException(string.Format("No message mapper registered for messages of type: {0}", typeof(T)));

            var message = messageMapper.MapToMessage(request);

            RetryAndBreakCircuit(() =>
                {
                    theMessageStore.Add(message, _messageStoreTimeout);
                    theMessageProducer.Send(message);
                });
        }

        /// <summary>
        /// Posts the specified request with async/await support. The message is placed on a task queue and into a message store for reposting in the event of failure.
        /// You will need to configure a service that reads from the task queue to process the message
        /// Paramore.Brighter.ServiceActivator provides an endpoint for use in a windows service that reads from a queue
        /// and then Sends or Publishes the message to a <see cref="CommandProcessor"/> within that service. The decision to <see cref="Send{T}"/> or <see cref="Publish{T}"/> is based on the
        /// mapper. Your mapper can map to a <see cref="Message"/> with either a <see cref="T:MessageType.MT_COMMAND"/> , which results in a <see cref="Send{T}(T)"/> or a
        /// <see cref="T:MessageType.MT_EVENT"/> which results in a <see cref="Publish{T}(T)"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="request">The request.</param>
        /// <param name="continueOnCapturedContext">Should we use the calling thread's synchronization context when continuing or a default thread synchronization context. Defaults to false</param>
        /// <exception cref="System.ArgumentOutOfRangeException"></exception>
        /// <returns>awaitable <see cref="Task"/>.</returns>
        public async Task PostAsync<T>(T request, bool continueOnCapturedContext = false) where T : class, IRequest
        {
            _logger.InfoFormat("Async decoupled invocation of request: {0}", request.Id);
            var theMessageStore = _messageStore as IAmAnAsyncMessageStore<Message>;
            if (theMessageStore == null)
                throw new ArgumentException("MessageStore does not implement IAmAnAsyncMessageStore");
            var theMessageProducer = _messageProducer as IAmAnAsyncMessageProducer;
            if (theMessageProducer == null)
                throw new ArgumentException("MessageProducer does not implement IAmAnAsyncMessageProducer");

            var messageMapper = _mapperRegistry.Get<T>();
            if (messageMapper == null)
                throw new ArgumentOutOfRangeException(string.Format("No message mapper registered for messages of type: {0}", typeof(T)));

            var message = messageMapper.MapToMessage(request);
            await RetryAndBreakCircuitAsync(async () =>
            {
                await theMessageStore.AddAsync(message, _messageStoreTimeout);
                await theMessageProducer.SendAsync(message);
            });
            await Task.Delay(0);
        }

        /// <summary>
        /// Posts the specified request, using the specified topic in the Message Header.
        /// Intended for use with Request-Reply scenarios instead of Publish-Subscribe scenarios
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="replyTo">Contains the topic used for routing the reply and the correlation id used by the sender to match response</param>
        /// <param name="request">The request.</param>
        /// <exception cref="System.ArgumentOutOfRangeException"></exception>
        public void Post<T>(ReplyAddress replyTo, T request) where T : class, IRequest
        {
            _logger.InfoFormat("Decoupled invocation of request: {0}", request.Id);
            var theMessageStore = _messageStore as IAmAMessageStore<Message>;
            if (theMessageStore == null)
                throw new ArgumentException("MessageStore does not implement IAmAMessageStore");
            var theMessageProducer = _messageProducer as IAmAMessageProducer;
            if (theMessageProducer == null)
                throw new ArgumentException("MessageProducer does not implement IAmAMessageProducer");

            if (request is IEvent)
                throw new ArgumentException("A Post that expects a Reply, should be a Command and not an Event", "request");

            var messageMapper = _mapperRegistry.Get<T>();
            if (messageMapper == null)
                throw new ArgumentOutOfRangeException(string.Format("No message mapper registered for messages of type: {0}", typeof(T)));

            var message = messageMapper.MapToMessage(request);
            message.Header.Topic = replyTo.Topic;
            message.Header.CorrelationId = replyTo.CorrelationId;

            RetryAndBreakCircuit(() =>
            {
                theMessageStore.Add(message, _messageStoreTimeout);
                theMessageProducer.Send(message);
            });
        }

        /// <summary>
        /// Posts the specified request, using the specified topic in the Message Header.
        /// Intended for use with Request-Reply scenarios instead of Publish-Subscribe scenarios
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="replyTo">Contains the topic used for routing the reply and the correlation id used by the sender to match response</param>
        /// <param name="request">The request.</param>
        /// <param name="continueOnCapturedContext">Should we use the calling thread's synchronization context when continuing or a default thread synchronization context. Defaults to false</param>
        /// <exception cref="System.ArgumentOutOfRangeException"></exception>
        public async Task PostAsync<T>(ReplyAddress replyTo, T request, bool continueOnCapturedContext = false) where T : class, IRequest
        {
            _logger.InfoFormat("Decoupled invocation of request: {0}", request.Id);
            var theMessageStore = _messageStore as IAmAnAsyncMessageStore<Message>;
            if (theMessageStore == null)
                throw new ArgumentException("MessageStore does not implement IAmAnAsyncMessageStore");
            var theMessageProducer = _messageProducer as IAmAnAsyncMessageProducer;
            if (theMessageProducer == null)
                throw new ArgumentException("MessageProducer does not implement IAmAnAsyncMessageProducer");

            if (request is IEvent)
                throw new ArgumentException("A Post that expects a Reply, should be a Command and not an Event", "request");

            var messageMapper = _mapperRegistry.Get<T>();
            if (messageMapper == null)
                throw new ArgumentOutOfRangeException(string.Format("No message mapper registered for messages of type: {0}", typeof(T)));

            var message = messageMapper.MapToMessage(request);
            message.Header.Topic = replyTo.Topic;
            message.Header.CorrelationId = replyTo.CorrelationId;

            await RetryAndBreakCircuitAsync(async () =>
            {
                await theMessageStore.AddAsync(message, _messageStoreTimeout);
                await theMessageProducer.SendAsync(message);
            });
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
                if (_messageProducer != null )
                    _messageProducer.Dispose();
            }

            _messageStore = null;
            _messageProducer = null; 

            _disposed = true;
        } 
        private void AssertValidSendPipeline<T>(T command, int handlerCount) where T : class, IRequest
        {
            _logger.InfoFormat("Found {0} pipelines for command: {1} {2}", handlerCount, typeof (T), command.Id);
            if (handlerCount > 1)
                throw new ArgumentException(
                    string.Format(
                        "More than one handler was found for the typeof command {0} - a command should only have one handler.",
                        typeof (T)));
            if (handlerCount == 0)
                throw new ArgumentException(
                    string.Format(
                        "No command handler was found for the typeof command {0} - a command should have exactly one handler.",
                        typeof (T)));
        }

        private void CheckCircuit(Action send)
        {
            _policyRegistry.Get(CIRCUITBREAKER).Execute(send);
        }

        private void RetryAndBreakCircuit(Action send)
        {
            CheckCircuit(() => Retry(send));
        }

        private void Retry(Action send)
        {
            _policyRegistry.Get(RETRYPOLICY).Execute(send);
        }

        private async Task RetryAsync(Func<Task> send)
        {
            await _policyRegistry.Get(RETRYPOLICY).Execute(send);
        }

        private async Task CheckCircuitAsync(Func<Task> send)
        {
            await _policyRegistry.Get(CIRCUITBREAKER).Execute(send);
        }

        private async Task RetryAndBreakCircuitAsync(Func<Task> send)
        {
            await CheckCircuitAsync(() => RetryAsync(send));
        }
    }
}
