using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus.ClientProvider
{
    public abstract class ServiceBusClientProvider : IServiceBusClientProvider
    {
        protected ServiceBusClient? Client { get; set; } 
        protected ServiceBusAdministrationClient? AdminClient { get; set; }
        
        /// <summary>
        /// Provides an Azure Service Bus Client
        /// </summary>
        /// <returns>Azure Service Bus Client</returns>
        public ServiceBusClient GetServiceBusClient() => Client ?? throw new ConfigurationException("ServiceBusClient is not configured");

        /// <summary>
        /// Provides an Azure Service Bus Administration Client
        /// </summary>
        /// <returns>Azure Service Bus Administration Client</returns>
        public ServiceBusAdministrationClient GetServiceBusAdministrationClient() => AdminClient ?? throw new ConfigurationException("ServiceBusAdministrationClient is not configured");
    }
}
