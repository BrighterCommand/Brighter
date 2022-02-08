using Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.ClientProvider;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus
{
    /// <summary>
    /// Factory class for creating instances of <see cref="AzureServiceBusMessageProducer"/>
    /// </summary>
    internal static class AzureServiceBusMessageProducerFactory
    {
        /// <summary>
        /// Factory to create an Azure Service Bus Producer
        /// </summary>
        /// <param name="configuration">The configuration to connect to <see cref="AzureServiceBusConfiguration"/></param>
        /// <param name="asbPublication">Describes the parameters for the producer</param>
        /// <returns>A Message Producer</returns>
        public static AzureServiceBusMessageProducer Get(
            AzureServiceBusConfiguration configuration,
            AzureServiceBusPublication asbPublication)
        {
            var clientProvider = new ServiceBusConnectionStringClientProvider(configuration.ConnectionString);
            return Get(clientProvider, asbPublication, configuration.BulkSendBatchSize);
        }

        /// <summary>
        /// Factory to create an Azure Service Bus Producer
        /// </summary>
        /// <param name="clientProvider">The connection to ASB</param>
        /// <param name="asbPublication">Describes the parameters for the producer</param>
        /// <param name="bulkSendBatchSize">When sending more than one message using the MessageProducer, the max amount to send in a single transmission.</param>
        /// <returns></returns>
        public static AzureServiceBusMessageProducer Get(
            IServiceBusClientProvider clientProvider,
            AzureServiceBusPublication asbPublication,
            int bulkSendBatchSize = 10)
        {
            var nameSpaceManagerWrapper = new AdministrationClientWrapper(clientProvider);
            var topicClientProvider = new ServiceBusSenderProvider(clientProvider);

            return new AzureServiceBusMessageProducer(nameSpaceManagerWrapper, topicClientProvider, asbPublication.MakeChannels, bulkSendBatchSize);
        }
    }
}
