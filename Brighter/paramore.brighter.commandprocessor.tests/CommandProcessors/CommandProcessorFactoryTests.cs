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
using FakeItEasy;
using Machine.Specifications;
using Polly;
using Raven.Client.Embedded;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.messagestore.ravendb;
using paramore.brighter.commandprocessor.messaginggateway.rmq;

namespace paramore.commandprocessor.tests.CommandProcessors
{
    public class When_creating_a_command_processor_by_a_factory
    {
        static CommandProcessorFactory commandProcessorFactory;
        static IAdaptAnInversionOfControlContainer container;
        static CommandProcessor commandProcessor; 

        private Establish context = () =>
            {
                container = A.Fake<IAdaptAnInversionOfControlContainer>();
                var logger = A.Fake<ILog>();
                A.CallTo(() => container.GetInstance<IAmARequestContextFactory>()).Returns(new InMemoryRequestContextFactory());
                A.CallTo(() => container.GetInstance<IAmAMessageStore<Message>>()).Returns(new RavenMessageStore(new EmbeddableDocumentStore(), logger));
                A.CallTo(() => container.GetInstance<IAmAMessagingGateway>()).Returns(new RMQMessagingGateway(logger));
                var retryPolicy = Policy
                    .Handle<Exception>()
                    .WaitAndRetry(new[]
                        {
                            TimeSpan.FromMilliseconds(50),
                            TimeSpan.FromMilliseconds(100),
                            TimeSpan.FromMilliseconds(150)
                        });
                A.CallTo(() => container.GetInstance<Policy>(CommandProcessor.RETRYPOLICY)).Returns(retryPolicy);

                var circuitBreakerPolicy = Policy
                    .Handle<Exception>()
                    .CircuitBreaker(1, TimeSpan.FromMilliseconds(500));
                A.CallTo(() => container.GetInstance<Policy>(CommandProcessor.CIRCUITBREAKER)).Returns(circuitBreakerPolicy);

                commandProcessorFactory = new CommandProcessorFactory(container);
            };

        Because of = () => commandProcessor = commandProcessorFactory.Create();

        It should_find_the_context_factory = () => A.CallTo(() => container.GetInstance<IAmARequestContextFactory>()).MustHaveHappened();
        It should_find_the_message_store = () => A.CallTo(() => container.GetInstance<IAmAMessageStore<Message>>()).MustHaveHappened();
        It should_find_the_messaging_gateway = () => A.CallTo(() => container.GetInstance<IAmAMessagingGateway>()).MustHaveHappened();
        It should_find_the_retry_policy = () => A.CallTo(() => container.GetInstance<Policy>(CommandProcessor.RETRYPOLICY));
        It should_find_the_circuit_breaker_policy = () => container.GetInstance<Policy>(CommandProcessor.CIRCUITBREAKER);
        It should_create_a_command_processor = () => commandProcessor.ShouldNotBeNull();
    }
}
