using Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus
{
    public static class AzureServiceBusMessageProducerFactory
    {
        public static AzureServiceBusMessageProducer Get(AzureServiceBusConfiguration configuration)
        {
            var nameSpaceManagerWrapper = new ManagementClientWrapper(configuration);
            var topicClientProvider = new TopicClientProvider(configuration);

            return new AzureServiceBusMessageProducer(nameSpaceManagerWrapper, topicClientProvider);
        }
    }
}
