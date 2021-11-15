using Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.ClientProvider;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus
{
    /// <summary>
    /// Factory class for creating instances of <see cref="AzureServiceBusMessageProducer"/>
    /// </summary>
    public static class AzureServiceBusMessageProducerFactory
    {
        /// <summary>
        /// Factory to create an Azure Service Bus Consumer
        /// </summary>
        /// <param name="configuration">The configuration to connect to <see cref="AzureServiceBusConfiguration"/></param>
        /// <param name="makeChannel">Mode to create channels <see cref="OnMissingChannel"/></param>
        /// <returns>A Message Producer</returns>
        public static AzureServiceBusMessageProducer Get(AzureServiceBusConfiguration configuration,
            OnMissingChannel makeChannel = OnMissingChannel.Create)
        {
            var clientProvider = new ServiceBusConnectionStringClientProvider(configuration.ConnectionString);
            return Get(clientProvider, makeChannel);
        }

        /// <summary>
        /// Factory to create an Azure Service Bus Consumer
        /// </summary>
        /// <param name="clientProvider">A client Provider <see cref="IServiceBusClientProvider"/> to determine how to connect to ASB</param>
        /// <param name="makeChannel">Mode to create channels <see cref="OnMissingChannel"/></param>
        /// <returns>A Message Producer</returns>
        public static AzureServiceBusMessageProducer Get(IServiceBusClientProvider clientProvider,
            OnMissingChannel makeChannel = OnMissingChannel.Create)
        {
            var nameSpaceManagerWrapper = new AdministrationClientWrapper(clientProvider);
            var topicClientProvider = new ServiceBusSenderProvider(clientProvider);

            return new AzureServiceBusMessageProducer(nameSpaceManagerWrapper, topicClientProvider, makeChannel);
        }
    }
}
