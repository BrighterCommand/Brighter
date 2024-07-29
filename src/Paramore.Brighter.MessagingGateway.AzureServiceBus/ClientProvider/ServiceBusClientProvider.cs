using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus.ClientProvider
{
    public abstract class ServiceBusClientProvider : IServiceBusClientProvider
    {
        protected abstract ServiceBusClient Client { get; }
        protected abstract ServiceBusAdministrationClient AdminClient { get; } 
        
        /// <summary>
        /// Provides an Azure Service Bus Client
        /// </summary>
        /// <returns>Azure Service Bus Client</returns>
        public ServiceBusClient GetServiceBusClient() => Client;

        /// <summary>
        /// Provides an Azure Service Bus Administration Client
        /// </summary>
        /// <returns>Azure Service Bus Administration Client</returns>
        public ServiceBusAdministrationClient GetServiceBusAdministrationClient() => AdminClient;
    }
}
