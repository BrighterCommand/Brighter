﻿using System;
using Azure.Messaging.ServiceBus;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.ClientProvider;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers
{
    internal class ServiceBusReceiverProvider : IServiceBusReceiverProvider
    {
        private readonly ServiceBusClient _client;

        public ServiceBusReceiverProvider(IServiceBusClientProvider clientProvider)
        {
            _client = clientProvider.GetServiceBusClient();
        }

        public IServiceBusReceiverWrapper Get(string topicName, string subscriptionName, ServiceBusReceiveMode receiveMode, bool sessionEnabled)
        {
            if (sessionEnabled)
            {
                try
                {
                    return new ServiceBusReceiverWrapper(_client.AcceptNextSessionAsync(topicName, subscriptionName,
                        new ServiceBusSessionReceiverOptions() {ReceiveMode = receiveMode}).GetAwaiter().GetResult());
                }
                catch (ServiceBusException e)
                {
                    if (e.Reason == ServiceBusFailureReason.ServiceTimeout)
                    {
                        //No session available
                        return null;
                    }

                    throw;
                }
            }
            else
            {
                return new ServiceBusReceiverWrapper(_client.CreateReceiver(topicName, subscriptionName,
                    new ServiceBusReceiverOptions { ReceiveMode = receiveMode, }));
            }
        }
    }
}
