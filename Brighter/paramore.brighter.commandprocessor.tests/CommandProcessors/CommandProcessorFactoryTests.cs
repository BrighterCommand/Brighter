using System;
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
                A.CallTo(() => container.GetInstance<IAmARequestContextFactory>()).Returns(new InMemoryRequestContextFactory());
                A.CallTo(() => container.GetInstance<IAmAMessageStore<Message>>()).Returns(new RavenMessageStore(new EmbeddableDocumentStore()));
                A.CallTo(() => container.GetInstance<IAmAMessagingGateway>()).Returns(new RMQMessagingGateway());
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
