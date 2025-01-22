using System;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus.ClientProvider
{
    /// <summary>
    /// Provides Azure Service Bus Clients using Default Azure Credentials.
    /// </summary>
    public class ServiceBusDefaultAzureClientProvider : ServiceBusClientProvider
    {
        /// <summary>
        /// Initializes an implementation is <see cref="IServiceBusClientProvider"/> using Default Azure Credentials for Authentication.
        /// </summary>
        /// <param name="fullyQualifiedNameSpace">The Fully Qualified Namespace i.e. my-servicebus.azureservicebus.net</param>
        /// <exception cref="ArgumentNullException">Throws is the namespace is null</exception>
        public ServiceBusDefaultAzureClientProvider(string fullyQualifiedNameSpace)
        {
            if (string.IsNullOrEmpty(fullyQualifiedNameSpace))
            {
                throw new ArgumentNullException(nameof(fullyQualifiedNameSpace),
                    "Fully qualified Namespace is null or empty, ensure this is set in the constructor.");
            }

            Client = new ServiceBusClient(fullyQualifiedNameSpace, new DefaultAzureCredential());
            AdminClient = new ServiceBusAdministrationClient(fullyQualifiedNameSpace,
                new DefaultAzureCredential());
        }
    }
}
