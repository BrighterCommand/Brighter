using Azure.Messaging.ServiceBus;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.ClientProvider;
using IServiceBusClientProvider = Paramore.Brighter.MessagingGateway.AzureServiceBus.ClientProvider.IServiceBusClientProvider;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus
{
    /// <summary>
    /// Factory class for creating instances of <see cref="AzureServiceBusConsumer"/>
    /// </summary>
    public class AzureServiceBusConsumerFactory : IAmAMessageConsumerFactory
    {
        private readonly IServiceBusClientProvider _clientProvider;
        private readonly bool _ackOnRead;

        /// <summary>
        /// Factory to create an Azure Service Bus Consumer
        /// </summary>
        /// <param name="configuration">The configuration to connect to <see cref="AzureServiceBusConfiguration"/></param>
        public AzureServiceBusConsumerFactory(AzureServiceBusConfiguration configuration)
        : this (new ServiceBusConnectionStringClientProvider(configuration.ConnectionString), configuration.AckOnRead)
        { }

        /// <summary>
        /// Factory to create an Azure Service Bus Consumer
        /// </summary>
        /// <param name="clientProvider">A client Provider <see cref="IServiceBusClientProvider"/> to determine how to connect to ASB</param>
        /// <param name="ackOnRead">Acknowledge Message on read (if set to false this will use a Peak Lock)</param>
        public AzureServiceBusConsumerFactory(IServiceBusClientProvider clientProvider, bool ackOnRead = false)
        {
            _ackOnRead = ackOnRead;
            _clientProvider = clientProvider;
        }

        /// <summary>
        /// Creates a consumer for the specified queue.
        /// </summary>
        /// <param name="subscription">The queue to connect to</param>
        /// <returns>IAmAMessageConsumer</returns>
        public IAmAMessageConsumer Create(Subscription subscription)
        {
            var nameSpaceManagerWrapper = new AdministrationClientWrapper(_clientProvider);

            var config = new AzureServiceBusSubscriptionConfiguration();
            
            if (subscription is AzureServiceBusSubscription sub) 
                config = sub.Configuration;
            
            return config.UseServiceBusQueue ? new AzureServiceBusConsumer(
                subscription.RoutingKey, 
                new AzureServiceBusMessageProducer(
                    nameSpaceManagerWrapper,
                    new ServiceBusSenderProvider(_clientProvider), 
                    new AzureServiceBusPublication{MakeChannels = subscription.MakeChannels}), 
                nameSpaceManagerWrapper,
                new ServiceBusReceiverProvider(_clientProvider),
                makeChannels: subscription.MakeChannels,
                receiveMode: _ackOnRead ? ServiceBusReceiveMode.ReceiveAndDelete : ServiceBusReceiveMode.PeekLock,
                batchSize: subscription.BufferSize,
                subscriptionConfiguration: config) : new AzureServiceBusConsumer(
                subscription.RoutingKey, 
                subscription.ChannelName,
                new AzureServiceBusMessageProducer(
                    nameSpaceManagerWrapper,
                    new ServiceBusSenderProvider(_clientProvider), 
                    new AzureServiceBusPublication{MakeChannels = subscription.MakeChannels}), 
                nameSpaceManagerWrapper,
                new ServiceBusReceiverProvider(_clientProvider),
                makeChannels: subscription.MakeChannels,
                receiveMode: _ackOnRead ? ServiceBusReceiveMode.ReceiveAndDelete : ServiceBusReceiveMode.PeekLock,
                batchSize: subscription.BufferSize,
                subscriptionConfiguration: config);
        }
    }
}
