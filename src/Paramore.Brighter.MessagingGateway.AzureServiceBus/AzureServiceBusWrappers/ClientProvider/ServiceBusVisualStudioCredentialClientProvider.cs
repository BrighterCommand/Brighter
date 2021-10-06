using System;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers.ClientProvider
{
    public class ServiceBusVisualStudioCredentialClientProvider: IServiceBusClientProvider
    {
        private readonly string _fullyQualifiedNameSpace;

        public ServiceBusVisualStudioCredentialClientProvider(string fullyQualifiedNameSpace)
        {
            if (string.IsNullOrEmpty(fullyQualifiedNameSpace))
            {
                throw new ArgumentNullException(nameof(fullyQualifiedNameSpace),
                    "Configuration is null, ensure this is set in the constructor.");
            }
            
            _fullyQualifiedNameSpace = fullyQualifiedNameSpace;
        }
        
        public ServiceBusClient GetServiceBusClient()
        {
            return new ServiceBusClient(_fullyQualifiedNameSpace, new VisualStudioCredential());
        }

        public ServiceBusAdministrationClient GetServiceBusAdministrationClient()
        {
            return new ServiceBusAdministrationClient(_fullyQualifiedNameSpace, new VisualStudioCredential());
        }
    }
}
