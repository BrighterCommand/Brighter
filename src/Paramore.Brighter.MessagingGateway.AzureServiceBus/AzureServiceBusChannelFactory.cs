using System;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus
{
    /// <summary>
    /// Creates instances of <see cref="IAmAChannelSync"/>channels using Azure Service Bus.
    /// </summary>
    public class AzureServiceBusChannelFactory : IAmAChannelFactory
    {
        private readonly AzureServiceBusConsumerFactory _azureServiceBusConsumerFactory;

        /// <summary>
        /// Initializes an Instance of <see cref="AzureServiceBusConsumerFactory"/>
        /// </summary>
        /// <param name="azureServiceBusConsumerFactory">An Azure Service Bus Consumer Factory.</param>
        public AzureServiceBusChannelFactory(AzureServiceBusConsumerFactory azureServiceBusConsumerFactory)
        {
            _azureServiceBusConsumerFactory = azureServiceBusConsumerFactory;
        }

        /// <summary>
        /// Creates the input channel.
        /// </summary>
        /// <param name="subscription">The parameters with which to create the channel for the transport</param>
        /// <returns>IAmAnInputChannel.</returns>
        public IAmAChannelSync CreateChannel(Subscription subscription)
        {
            if (!(subscription is AzureServiceBusSubscription azureServiceBusSubscription))
            {
                throw new ConfigurationException("We expect an AzureServiceBusSubscription or AzureServiceBusSubscription<T> as a parameter");
            }

            if (subscription.TimeOut < TimeSpan.FromMilliseconds(400))
            {
                throw new ArgumentException("The minimum allowed timeout is 400 milliseconds");
            }

            IAmAMessageConsumerSync messageConsumer =
                _azureServiceBusConsumerFactory.Create(azureServiceBusSubscription);

            return new Channel(
                channelName: subscription.ChannelName, 
                routingKey: subscription.RoutingKey,
                messageConsumer: messageConsumer,
                maxQueueLength: subscription.BufferSize
            );
        }

        /// <summary>
        /// Creates the input channel.
        /// </summary>
        /// <param name="subscription">The parameters with which to create the channel for the transport</param>
        /// <returns>IAmAnInputChannel.</returns>
        public IAmAChannelAsync CreateChannelAsync(Subscription subscription)
        {
            if (!(subscription is AzureServiceBusSubscription azureServiceBusSubscription))
            {
                throw new ConfigurationException("We expect an AzureServiceBusSubscription or AzureServiceBusSubscription<T> as a parameter");
            }

            if (subscription.TimeOut < TimeSpan.FromMilliseconds(400))
            {
                throw new ArgumentException("The minimum allowed timeout is 400 milliseconds");
            }

            IAmAMessageConsumerAsync messageConsumer =
                _azureServiceBusConsumerFactory.CreateAsync(azureServiceBusSubscription);

            return new ChannelAsync(
                channelName: subscription.ChannelName,
                routingKey: subscription.RoutingKey,
                messageConsumer: messageConsumer,
                maxQueueLength: subscription.BufferSize
                );
        }
    }
}
