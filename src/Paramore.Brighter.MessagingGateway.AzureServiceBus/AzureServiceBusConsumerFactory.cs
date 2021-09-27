using Microsoft.Azure.ServiceBus;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus
{
    public class AzureServiceBusConsumerFactory : IAmAMessageConsumerFactory
    {
        private readonly AzureServiceBusConfiguration _configuration;

        public AzureServiceBusConsumerFactory(AzureServiceBusConfiguration configuration)
        {
            _configuration = configuration;
        }
        
        public IAmAMessageConsumer Create(Subscription subscription)
        {
            return Create(subscription, _configuration);
        }

        public static IAmAMessageConsumer Create(Subscription subscription, AzureServiceBusConfiguration configuration)
        {
            var nameSpaceManagerWrapper = new ManagementClientWrapper(configuration);

            return new AzureServiceBusConsumer(subscription.RoutingKey, subscription.ChannelName,
                new AzureServiceBusMessageProducer(nameSpaceManagerWrapper,
                    new TopicClientProvider(configuration), subscription.MakeChannels), nameSpaceManagerWrapper,
                new MessageReceiverProvider(configuration),
                makeChannels: subscription.MakeChannels,
                receiveMode: configuration.AckOnRead ? ReceiveMode.PeekLock : ReceiveMode.ReceiveAndDelete,
                batchSize: subscription.BufferSize);
        }
    }
}
