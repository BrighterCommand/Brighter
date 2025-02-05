using System;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus.ClientProvider
{
    /// <summary>
    /// Provides Azure Service Bus Clients using Managed Identity Credentials.
    /// </summary>
    public class ServiceBusManagedIdentityClientProvider : ServiceBusClientProvider
    {
        /// <summary>
        /// Initializes an implementation is <see cref="IServiceBusClientProvider"/> using Managed Identity for Authentication.
        /// </summary>
        /// <param name="fullyQualifiedNameSpace">The Fully Qualified Namespace i.e. my-servicebus.azureservicebus.net</param>
        /// <exception cref="ArgumentNullException">Throws is the namespace is null</exception>
        public ServiceBusManagedIdentityClientProvider(string fullyQualifiedNameSpace)
        {
            if (string.IsNullOrEmpty(fullyQualifiedNameSpace))
            {
                throw new ArgumentNullException(nameof(fullyQualifiedNameSpace),
                    "Fully qualified Namespace is null or empty, ensure this is set in the constructor.");
            }
            
            Client = new ServiceBusClient(fullyQualifiedNameSpace, new ManagedIdentityCredential());
            AdminClient = new ServiceBusAdministrationClient(fullyQualifiedNameSpace,
                new ManagedIdentityCredential());
        }
    }
}
