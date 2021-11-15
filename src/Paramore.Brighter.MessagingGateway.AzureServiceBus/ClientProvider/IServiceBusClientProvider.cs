using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus.ClientProvider
{
    /// <summary>
    /// Provides Clients for Azure Service Bus
    /// </summary>
    public interface IServiceBusClientProvider
    {
        /// <summary>
        /// Provides an Azure Service Bus Client
        /// </summary>
        /// <returns>Azure Service Bus Client</returns>
        ServiceBusClient GetServiceBusClient();
        /// <summary>
        /// Provides an Azure Service Bus Administration Client
        /// </summary>
        /// <returns>Azure Service Bus Administration Client</returns>
        ServiceBusAdministrationClient GetServiceBusAdministrationClient();
    }
}
