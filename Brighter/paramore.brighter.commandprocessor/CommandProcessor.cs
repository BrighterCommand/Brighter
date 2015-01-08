// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor
// Author           : ian
// Created          : 07-01-2014
//
// Last Modified By : ian
// Last Modified On : 07-29-2014
// ***********************************************************************
// <copyright file="CommandProcessor.cs" company="">
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
using System.Linq;
using Common.Logging;
using Polly;
using paramore.brighter.commandprocessor.extensions;

namespace paramore.brighter.commandprocessor
{
    /// <summary>
    /// Class CommandProcessor.
    /// Implements both the <a href="http://www.hillside.net/plop/plop2001/accepted_submissions/PLoP2001/bdupireandebfernandez0/PLoP2001_bdupireandebfernandez0_1.pdf">Command Dispatcher</a> 
    /// and <a href="http://wiki.hsr.ch/APF/files/CommandProcessor.pdf">Command Processor</a> Design Patterns 
    /// </summary>
    public class CommandProcessor : IAmACommandProcessor
    {
        readonly IAmAMessageMapperRegistry mapperRegistry;
        readonly IAmASubscriberRegistry subscriberRegistry;
        readonly IAmAHandlerFactory handlerFactory;
        readonly IAmARequestContextFactory requestContextFactory;
        readonly IAmAPolicyRegistry policyRegistry;
        readonly ILog logger;
        readonly IAmAMessageStore<Message> messageStore;
        readonly IAmAMessageProducer messagingGateway;
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
            this.subscriberRegistry = subscriberRegistry;
            this.handlerFactory = handlerFactory;
            this.requestContextFactory = requestContextFactory;
            this.policyRegistry = policyRegistry;
            this.logger = logger;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandProcessor"/> class.
        /// Use this constructor when only task queue support is required
        /// </summary>
        /// <param name="requestContextFactory">The request context factory.</param>
        /// <param name="policyRegistry">The policy registry.</param>
        /// <param name="mapperRegistry">The mapper registry.</param>
        /// <param name="messageStore">The message store.</param>
        /// <param name="messagingGateway">The messaging gateway.</param>
        /// <param name="logger">The logger.</param>
        public CommandProcessor(
            IAmARequestContextFactory requestContextFactory, 
            IAmAPolicyRegistry policyRegistry,
            IAmAMessageMapperRegistry mapperRegistry, 
            IAmAMessageStore<Message> messageStore, 
            IAmAMessageProducer messagingGateway,
            ILog logger)
        {
            this.requestContextFactory = requestContextFactory;
            this.policyRegistry = policyRegistry;
            this.logger = logger;
            this.mapperRegistry = mapperRegistry;
            this.messageStore = messageStore;
            this.messagingGateway = messagingGateway;
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
        /// <param name="messagingGateway">The messaging gateway.</param>
        /// <param name="logger">The logger.</param>
        public CommandProcessor(
            IAmASubscriberRegistry subscriberRegistry,
            IAmAHandlerFactory handlerFactory,
            IAmARequestContextFactory requestContextFactory, 
            IAmAPolicyRegistry policyRegistry,
            IAmAMessageMapperRegistry mapperRegistry, 
            IAmAMessageStore<Message> messageStore, 
            IAmAMessageProducer messagingGateway,
            ILog logger)
            :this(subscriberRegistry, handlerFactory, requestContextFactory, policyRegistry, logger)
        {
            this.mapperRegistry = mapperRegistry;
            this.messageStore = messageStore;
            this.messagingGateway = messagingGateway;
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
            using (var builder = new PipelineBuilder<T>(subscriberRegistry, handlerFactory, logger))
            {
                var requestContext = requestContextFactory.Create();
                requestContext.Policies = policyRegistry;

                logger.Info(m => m("Building send pipeline for command: {0}", command.Id));
                var handlerChain = builder.Build(requestContext);

                var handlerCount = handlerChain.Count();

                logger.Info(m => m("Found {0} pipelines for command: {1} {2}", handlerCount, typeof(T), command.Id));
                if (handlerCount > 1)
                    throw new ArgumentException(string.Format("More than one handler was found for the typeof command {0} - a command should only have one handler.", typeof (T)));
                if (handlerCount == 0)
                    throw new ArgumentException(string.Format("No command handler was found for the typeof command {0} - a command should have only one handler.",typeof (T)));

                handlerChain.First().Handle(command);
            }
        }

        /// <summary>
        /// Publishes the specified event. We expect zero or more handlers. The events are handled synchronously, in turn
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="event">The event.</param>
        public void Publish<T>(T @event) where T : class, IRequest
        {
            using (var builder = new PipelineBuilder<T>(subscriberRegistry, handlerFactory, logger))
            {
                var requestContext = requestContextFactory.Create();
                requestContext.Policies = policyRegistry;

                logger.Info(m => m("Building send pipeline for command: {0}", @event.Id));
                var handlerChain = builder.Build(requestContext);

                var handlerCount = handlerChain.Count();

                logger.Info(m => m("Found {0} pipelines for command: {0}", handlerCount, @event.Id));

                handlerChain.Each(chain => chain.Handle(@event));
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
            logger.Info(m => m("Decoupled invocation of request: {0}", request.Id));

            var messageMapper = mapperRegistry.Get<T>();
            if (messageMapper == null)
                throw new ArgumentOutOfRangeException(string.Format("No message mapper registered for messages of type: {0}", typeof(T)));

            var message = messageMapper.MapToMessage(request);
            /* 
             * NOTE: Don't rewrite with await, compiles but Policy does not call await on the lambda so becomes fire and forget, 
             * see http://blogs.msdn.com/b/pfxteam/archive/2012/02/08/10265476.aspx
            */
            RetryAndBreakCircuit(() =>
                {
                    messageStore.Add(message).Wait();
                    messagingGateway.Send(message).Wait();
                });
        }

        /// <summary>
        /// Reposts the specified message identifier. It retrieves a message previously posted via Post, from the Message Store, and publishes it 
        /// onto the Work Queue again.
        /// </summary>
        /// <param name="messageId">The message identifier.</param>
        public void Repost(Guid messageId)
        {
            var requestedMessageid = messageId; //avoid closure on this
            logger.Info(m => m("Resend of message: {0}", requestedMessageid));

            /* 
             * NOTE: Don't rewrite with await, compiles but Policy does not call await on the lambda so becomes fire and forget, 
             * see http://blogs.msdn.com/b/pfxteam/archive/2012/02/08/10265476.aspx
            */
            RetryAndBreakCircuit(() =>
                { 
                    var task = messageStore.Get(messageId);
                    task.Wait();

                    var message = task.Result;

                    if (message.Header.MessageType == MessageType.MT_EMPTY)
                    {
                        logger.Warn((m => m("Message {0} not found", requestedMessageid)));
                        return;
                    }

                    messagingGateway.Send(message);
                });
        }

        void RetryAndBreakCircuit(Action send)
        {
            CheckCircuit(() => Retry(send));
        }

        void CheckCircuit(Action send)
        {
            policyRegistry.Get(CIRCUITBREAKER).Execute(send);
        }

        void Retry(Action send)
        {
            policyRegistry.Get(RETRYPOLICY).Execute(send);
        }
    }
}