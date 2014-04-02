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
            var requestContextFactory = container.GetInstance<IAmARequestContextFactory>();
            var messageStore = container.GetInstance<IAmAMessageStore<Message>>();
            var messagingGateway = container.GetInstance<IAmAMessagingGateway>();
            var retryPolicy = container.GetInstance<Policy>(CommandProcessor.RETRYPOLICY);
            var circuitBreakerPolicy = container.GetInstance<Policy>(CommandProcessor.CIRCUITBREAKER);

            return new CommandProcessor(
                container: container,
                requestContextFactory: requestContextFactory,
                messageStore: messageStore,
                messagingGateway: messagingGateway,
                retryPolicy: retryPolicy,
                circuitBreakerPolicy: circuitBreakerPolicy);
        }
    }
}