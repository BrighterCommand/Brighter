using System;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus
{
    public class AzureServiceBusChannelFactory : IAmAChannelFactory
    {
        private readonly AzureServiceBusConsumerFactory _azureServiceBusConsumerFactory;
        
        public AzureServiceBusChannelFactory(AzureServiceBusConsumerFactory azureServiceBusConsumerFactory)
        {
            _azureServiceBusConsumerFactory = azureServiceBusConsumerFactory;
        }
        
        public IAmAChannel CreateChannel(Subscription subscription)
        {
            if (!(subscription is AzureServiceBusSubscription azureServiceBusSubscription))
            {
                throw new ConfigurationException("We expect an AzureServiceBusSubscription or AzureServiceBusSubscription<T> as a parameter");
            }

            if (subscription.TimeoutInMiliseconds < 400)
            {
                throw new ArgumentException("The minimum allowed timeout is 400 milliseconds");
            }

            IAmAMessageConsumer messageConsumer = _azureServiceBusConsumerFactory.Create(azureServiceBusSubscription);

            return new Channel(
                channelName: subscription.ChannelName,
                messageConsumer: messageConsumer,
                maxQueueLength: subscription.BufferSize
            );
        }
    }
}
