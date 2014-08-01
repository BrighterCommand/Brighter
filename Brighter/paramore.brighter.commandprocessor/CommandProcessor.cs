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

/// <summary>
/// The commandprocessor namespace.{CC2D43FA-BBC4-448A-9D0B-7B57ADF2655C}
/// </summary>
namespace paramore.brighter.commandprocessor
{
    /// <summary>
    /// Class CommandProcessor.{CC2D43FA-BBC4-448A-9D0B-7B57ADF2655C}
    /// </summary>
    public class CommandProcessor : IAmACommandProcessor
    {
        readonly IAmAMessageMapperRegistry mapperRegistry;
        readonly IAmASubscriberRegistry subscriberRegistry;
        private readonly IAmAHandlerFactory handlerFactory;
        readonly IAmARequestContextFactory requestContextFactory;
        private readonly IAmAPolicyRegistry policyRegistry;
        readonly ILog logger;
        readonly IAmAMessageStore<Message> messageStore;
        readonly IAmASendMessageGateway messagingGateway;
        /// <summary>
        /// The circuitbreaker{CC2D43FA-BBC4-448A-9D0B-7B57ADF2655C}
        /// </summary>
        public const string CIRCUITBREAKER = "Paramore.Brighter.CommandProcessor.CircuitBreaker";
        /// <summary>
        /// The retrypolicy{CC2D43FA-BBC4-448A-9D0B-7B57ADF2655C}
        /// </summary>
        public const string RETRYPOLICY = "Paramore.Brighter.CommandProcessor.RetryPolicy";

        //use when no task queue support required
        /// <summary>
        /// Initializes a new instance of the <see cref="CommandProcessor"/> class.
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

        //Use when only task queue support required
        /// <summary>
        /// Initializes a new instance of the <see cref="CommandProcessor"/> class.
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
            IAmASendMessageGateway messagingGateway,
            ILog logger)
        {
            this.requestContextFactory = requestContextFactory;
            this.policyRegistry = policyRegistry;
            this.logger = logger;
            this.mapperRegistry = mapperRegistry;
            this.messageStore = messageStore;
            this.messagingGateway = messagingGateway;
        }

        //Use when task queue and command processor support required
        /// <summary>
        /// Initializes a new instance of the <see cref="CommandProcessor"/> class.
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
            IAmASendMessageGateway messagingGateway,
            ILog logger)
            :this(subscriberRegistry, handlerFactory, requestContextFactory, policyRegistry, logger)
        {
            this.mapperRegistry = mapperRegistry;
            this.messageStore = messageStore;
            this.messagingGateway = messagingGateway;
        }


        /// <summary>
        /// Sends the specified command.
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

                logger.Info(m => m("Found {0} pipelines for command: {1}", handlerCount, command.Id));
                if (handlerCount > 1)
                    throw new ArgumentException(string.Format("More than one handler was found for the typeof command {0} - a command should only have one handler.", typeof (T)));
                if (handlerCount == 0)
                    throw new ArgumentException(string.Format("No command handler was found for the typeof command {0} - a command should have only one handler.",typeof (T)));

                handlerChain.First().Handle(command);
            }
        }

        /// <summary>
        /// Publishes the specified event.
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

        //NOTE: Don't rewrite with await, compiles but Policy does not call await on the lambda so becomes fire and forget, see http://blogs.msdn.com/b/pfxteam/archive/2012/02/08/10265476.aspx

        /// <summary>
        /// Posts the specified request.
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
            RetryAndBreakCircuit(() =>
                {
                    messageStore.Add(message).Wait();
                    messagingGateway.Send(message).Wait();
                });
        }

        /// <summary>
        /// Reposts the specified message identifier.
        /// </summary>
        /// <param name="messageId">The message identifier.</param>
        public void Repost(Guid messageId)
        {
            var requestedMessageid = messageId; //avoid closure on this
            logger.Info(m => m("Resend of request: {0}", requestedMessageid));

            RetryAndBreakCircuit(() =>
                { 
                    var task = messageStore.Get(messageId);
                    task.Wait();
                    var message = task.Result;
                    messagingGateway.Send(message);
                });
        }

        private void RetryAndBreakCircuit(Action send)
        {
            CheckCircuit(() => Retry(send));
        }

        private void CheckCircuit(Action send)
        {
            policyRegistry.Get(CIRCUITBREAKER).Execute(send);
        }

        private void Retry(Action send)
        {
            policyRegistry.Get(RETRYPOLICY).Execute(send);
        }
    }
}