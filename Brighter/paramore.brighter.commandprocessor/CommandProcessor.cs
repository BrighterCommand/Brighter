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
    public class CommandProcessor : IAmACommandProcessor
    {
        readonly IAdaptAnInversionOfControlContainer container;
        readonly IAmARequestContextFactory requestContextFactory;
        readonly ILog logger;
        readonly IAmAMessageStore<Message> messageStore;
        readonly IAmAMessagingGateway messagingGateway;
        readonly Policy retryPolicy;
        readonly Policy circuitBreakerPolicy;
        public const string CIRCUITBREAKER = "Paramore.Brighter.CommandProcessor.CircuitBreaker";
        public const string RETRYPOLICY = "Paramore.Brighter.CommandProcessor.RetryPolicy";

        public CommandProcessor(IAdaptAnInversionOfControlContainer container, IAmARequestContextFactory requestContextFactory, ILog logger)
        {
            this.container = container;
            this.requestContextFactory = requestContextFactory;
            this.logger = logger;
        }

        public CommandProcessor(
            IAdaptAnInversionOfControlContainer container, 
            IAmARequestContextFactory requestContextFactory, 
            IAmAMessageStore<Message> messageStore, 
            IAmAMessagingGateway messagingGateway,
            Policy retryPolicy,
            Policy circuitBreakerPolicy, 
            ILog logger)
            :this(container, requestContextFactory, logger)
        {
            this.messageStore = messageStore;
            this.messagingGateway = messagingGateway;
            this.retryPolicy = retryPolicy;
            this.circuitBreakerPolicy = circuitBreakerPolicy;
        }


        public void Send<T>(T command) where T : class, IRequest
        {
            using (var builder = new PipelineBuilder<T>(container, logger))
            {
                var requestContext = requestContextFactory.Create(container);

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

        public void Publish<T>(T @event) where T : class, IRequest
        {
            using (var builder = new PipelineBuilder<T>(container, logger))
            {
                var requestContext = new RequestContext(container);

                logger.Info(m => m("Building send pipeline for command: {0}", @event.Id));
                var handlerChain = builder.Build(requestContext);

                var handlerCount = handlerChain.Count();

                logger.Info(m => m("Found {0} pipelines for command: {0}", handlerCount, @event.Id));

                handlerChain.Each(chain => chain.Handle(@event));
            }
        }

        //NOTE: Don't rewrite with await, compiles but Policy does not call await on the lambda so becomes fire and forget, see http://blogs.msdn.com/b/pfxteam/archive/2012/02/08/10265476.aspx

        public void Post<T>(T request) where T : class, IRequest
        {
            logger.Info(m => m("Decoupled invocation of request: {0}", request.Id));

            var messageMapper = container.GetInstance<IAmAMessageMapper<T>>();
            var message = messageMapper.MapToMessage(request);
            RetryAndBreakCircuit(() =>
                {
                    messageStore.Add(message).Wait();
                    messagingGateway.SendMessage(message).Wait();
                });
        }

        public void Repost(Guid messageId)
        {
            var requestedMessageid = messageId; //avoid closure on this
            logger.Info(m => m("Resend of request: {0}", requestedMessageid));

            RetryAndBreakCircuit(() =>
                { 
                    var task = messageStore.Get(messageId);
                    task.Wait();
                    var message = task.Result;
                    messagingGateway.SendMessage(message);
                });
        }

        private void RetryAndBreakCircuit(Action send)
        {
            CheckCircuit(() => Retry(send));
        }

        private void CheckCircuit(Action send)
        {
            circuitBreakerPolicy.Execute(send);
        }

        private void Retry(Action send)
        {
            retryPolicy.Execute(send);
        }
    }
}