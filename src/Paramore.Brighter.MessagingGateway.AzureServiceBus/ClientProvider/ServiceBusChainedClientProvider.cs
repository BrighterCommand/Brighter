using System;
using Azure.Core;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus.ClientProvider
{
    public class ServiceBusChainedClientProvider : IServiceBusClientProvider
    {
        private readonly string _fullyQualifiedNameSpace;

        private readonly ChainedTokenCredential _tokenCredential;

        /// <summary>
        /// Initializes an implementation is <see cref="IServiceBusClientProvider"/> using Default Azure Credentials for Authentication.
        /// </summary>
        /// <param name="fullyQualifiedNameSpace">The Fully Qualified Namespace i.e. my-servicebus.azureservicebus.net</param>
        /// <param name="credentialSources">List of Token Providers to use when trying to obtain a token.</param>
        /// <exception cref="ArgumentNullException">Throws is the namespace is null</exception>
        public ServiceBusChainedClientProvider(string fullyQualifiedNameSpace, params TokenCredential[] credentialSources)
        {
            if (string.IsNullOrEmpty(fullyQualifiedNameSpace))
            {
                throw new ArgumentNullException(nameof(fullyQualifiedNameSpace),
                    "Fully qualified Namespace is null or empty, ensure this is set in the constructor.");
            }
            else if (credentialSources == null || credentialSources.Length < 1)
            {
                throw new ArgumentNullException(nameof(credentialSources),
                    "Credential Sources is null or empty, ensure this is set in the constructor.");
            }

            _tokenCredential = new ChainedTokenCredential(credentialSources);
            _fullyQualifiedNameSpace = fullyQualifiedNameSpace;
        }

        /// <summary>
        /// Provides an Azure Service Bus Client
        /// </summary>
        /// <returns>Azure Service Bus Client</returns>
        public ServiceBusClient GetServiceBusClient()
        {
            return new ServiceBusClient(_fullyQualifiedNameSpace, _tokenCredential);
        }

        /// <summary>
        /// Provides an Azure Service Bus Administration Client
        /// </summary>
        /// <returns>Azure Service Bus Administration Client</returns>
        public ServiceBusAdministrationClient GetServiceBusAdministrationClient()
        {
            return new ServiceBusAdministrationClient(_fullyQualifiedNameSpace, _tokenCredential);
        }
    }
}
