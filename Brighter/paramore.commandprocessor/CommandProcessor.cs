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
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Polly;
using paramore.brighter.commandprocessor.extensions;

namespace paramore.brighter.commandprocessor
{
    public class CommandProcessor : IAmACommandProcessor
    {
        readonly IAdaptAnInversionOfControlContainer container;
        readonly IAmARequestContextFactory requestContextFactory;
        readonly IAmAMessageStore<Message> messageStore;
        readonly IAmAMessagingGateway messagingGateway;
        readonly Policy retryPolicy;
        readonly Policy circuitBreakerPolicy;
        public const string CIRCUITBREAKER = "Paramore.Brighter.CommandProcessor.CircuitBreaker";
        public const string RETRYPOLICY = "Paramore.Brighter.CommandProcessor.RetryPolicy";

        public CommandProcessor(IAdaptAnInversionOfControlContainer container, IAmARequestContextFactory requestContextFactory)
        {
            this.container = container;
            this.requestContextFactory = requestContextFactory;
        }

        public CommandProcessor(IAdaptAnInversionOfControlContainer container, IAmARequestContextFactory requestContextFactory, IAmAMessageStore<Message> messageStore, IAmAMessagingGateway messagingGateway)
            :this(container, requestContextFactory)
        {
            this.messageStore = messageStore;
            this.messagingGateway = messagingGateway;
            retryPolicy = container.GetInstance<Policy>(RETRYPOLICY);
            Debug.Assert(retryPolicy != null, "Provide a policy for retrying failed posts and reposts");
            circuitBreakerPolicy = container.GetInstance<Policy>(CIRCUITBREAKER);
        }


        public void Send<T>(T command) where T : class, IRequest
        {
            using (var builder = new PipelineBuilder<T>(container))
            {
                var requestContext = requestContextFactory.Create(container);
                var handlerChain = builder.Build(requestContext);

                var handlerCount = handlerChain.Count();

                if (handlerCount > 1)
                    throw new ArgumentException(string.Format("More than one handler was found for the typeof command {0} - a command should only have one handler.", typeof (T)));
                if (handlerCount == 0)
                    throw new ArgumentException(string.Format("No command handler was found for the typeof command {0} - a command should have only one handler.",typeof (T)));

                handlerChain.First().Handle(command);
            }
        }

        public void Publish<T>(T @event) where T : class, IRequest
        {
            using (var builder = new PipelineBuilder<T>(container))
            {
                var requestContext = new RequestContext(container);
                var handlerChain = builder.Build(requestContext);

                handlerChain.Each(chain => chain.Handle(@event));
            }
        }

        public void Post<T>(T request) where T : class, IRequest
        {
            //TODO: Make the call async 
            var messageMapper = container.GetInstance<IAmAMessageMapper<T, Message>>();
            var message = messageMapper.Map(request);
            messageStore.Add(message);
            RetryAndBreakCircuit(() => messagingGateway.SendMessage(message));
        }

        public void Repost(Guid messageId)
        {
            var message = messageStore.Get(messageId);
            RetryAndBreakCircuit(() => messagingGateway.SendMessage(message));
        }

        private void RetryAndBreakCircuit(Action send)
        {
            CheckCircuit(() => Retry(send));
        }

        private void CheckCircuit(Action send)
        {
            circuitBreakerPolicy.Execute(() => send());
        }

        private void Retry(Action send)
        {
            retryPolicy.Execute(() => send());
        }
    }
}