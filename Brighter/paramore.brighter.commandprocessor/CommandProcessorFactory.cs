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
using Common.Logging;
using Polly;

namespace paramore.brighter.commandprocessor
{
    public class CommandProcessorFactory : IAmACommandProcessorFactory
    {
        private readonly IAdaptAnInversionOfControlContainer container;

        public CommandProcessorFactory(IAdaptAnInversionOfControlContainer container)
        {
            this.container = container;
        }

        public CommandProcessor Create()
        {
            var requestContextFactory = RequestContextFactory();
            var messageStore = MessageStore();
            var messagingGateway = MessagingGateway();
            var retryPolicy = GetInstance();
            var circuitBreakerPolicy = CircuitBreakerPolicy();
            var logger = Logger();

            return new CommandProcessor(
                container: container,
                requestContextFactory: requestContextFactory,
                messageStore: messageStore,
                messagingGateway: messagingGateway,
                retryPolicy: retryPolicy,
                circuitBreakerPolicy: circuitBreakerPolicy,
                logger: logger);
        }

        private ILog Logger()
        {
            return container.GetInstance<ILog>();
        }

        private Policy CircuitBreakerPolicy()
        {
            try
            {
                return container.GetInstance<Policy>(CommandProcessor.CIRCUITBREAKER);
            }
            catch(Exception)
            {
                return null;
            }
        }

        private Policy GetInstance()
        {
            try
            {
                return container.GetInstance<Policy>(CommandProcessor.RETRYPOLICY);
            }
            catch (Exception)
            {

                return null;
            }
            
        }

        private IAmAMessagingGateway MessagingGateway()
        {
            try
            {
                return container.GetInstance<IAmAMessagingGateway>();
            }
            catch (Exception)
            {
                return null;
            }
            
        }

        private IAmAMessageStore<Message> MessageStore()
        {
            try
            {
                return container.GetInstance<IAmAMessageStore<Message>>();
            }
            catch (Exception)
            {

                return null;
            }
            
        }

        private IAmARequestContextFactory RequestContextFactory()
        {
            try
            {
                return container.GetInstance<IAmARequestContextFactory>();
            }
            catch (Exception)
            {

                return null;
            }
            
        }
    }
}