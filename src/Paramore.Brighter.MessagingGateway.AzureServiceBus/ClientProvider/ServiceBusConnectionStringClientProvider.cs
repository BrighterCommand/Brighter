﻿using System;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus.ClientProvider
{
    /// <summary>
    /// Provides Azure Service Bus Clients using a Connection String.
    /// </summary>
    public class ServiceBusConnectionStringClientProvider : IServiceBusClientProvider
    {
        private readonly string _connectionString;

        /// <summary>
        /// Initializes an implementation is <see cref="IServiceBusClientProvider"/> using a connection string.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <exception cref="ArgumentNullException">Throws is the namespace is null</exception>
        public ServiceBusConnectionStringClientProvider(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentNullException(nameof(connectionString),
                    "Configuration is null, ensure this is set in the constructor.");
            }

            _connectionString = connectionString;
        }

        /// <summary>
        /// Provides an Azure Service Bus Client
        /// </summary>
        /// <returns>Azure Service Bus Client</returns>
        public ServiceBusClient GetServiceBusClient()
        {
            return new ServiceBusClient(_connectionString);
        }
        /// <summary>
        /// Provides an Azure Service Bus Administration Client
        /// </summary>
        /// <returns>Azure Service Bus Administration Client</returns>
        public ServiceBusAdministrationClient GetServiceBusAdministrationClient()
        {
            return new ServiceBusAdministrationClient(_connectionString);
        }
    }
}
