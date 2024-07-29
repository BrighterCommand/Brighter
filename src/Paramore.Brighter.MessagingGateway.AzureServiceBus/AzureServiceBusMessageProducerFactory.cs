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

            if(asbPublication.UseServiceBusQueue)
                return new AzureServiceBusQueueMessageProducer(nameSpaceManagerWrapper, topicClientProvider, asbPublication, bulkSendBatchSize);
            else
                return new AzureServiceBusTopicMessageProducer(nameSpaceManagerWrapper, topicClientProvider, asbPublication, bulkSendBatchSize);
        }
    }
}
