using System;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers.ClientProvider
{
    public class ServiceBusConnectionStringClientProvider : IServiceBusClientProvider
    {
        private readonly string _connectionString;

        public ServiceBusConnectionStringClientProvider(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentNullException(nameof(connectionString),
                    "Configuration is null, ensure this is set in the constructor.");
            }
            
            _connectionString = connectionString;
        }

        public ServiceBusClient GetServiceBusClient()
        {
            return new ServiceBusClient(_connectionString);
        }

        public ServiceBusAdministrationClient GetServiceBusAdministrationClient()
        {
            return new ServiceBusAdministrationClient(_connectionString);
        }
    }
}
