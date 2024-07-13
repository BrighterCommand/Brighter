using System;
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
        
        /// <summary>
        /// Gets a <see cref="IServiceBusReceiverWrapper"/> for a Service Bus Queue
        /// </summary>
        /// <param name="queueName">The name of the Topic.</param>
        /// <param name="receiveMode">The Receive Mode.</param>
        /// <param name="sessionEnabled">Use Sessions for Processing</param>
        /// <returns>A ServiceBusReceiverWrapper.</returns>
        public IServiceBusReceiverWrapper Get(string queueName, ServiceBusReceiveMode receiveMode, bool sessionEnabled)
        {
            if (sessionEnabled)
            {
                try
                {
                    return new ServiceBusReceiverWrapper(_client.AcceptNextSessionAsync(queueName,
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
                return new ServiceBusReceiverWrapper(_client.CreateReceiver(queueName,
                    new ServiceBusReceiverOptions { ReceiveMode = receiveMode, }));
            }
        }

        /// <summary>
        /// Gets a <see cref="IServiceBusReceiverWrapper"/> for a Service Bus Topic
        /// </summary>
        /// <param name="topicName">The name of the Topic.</param>
        /// <param name="subscriptionName">The name of the Subscription on the Topic.</param>
        /// <param name="receiveMode">The Receive Mode.</param>
        /// <param name="sessionEnabled">Use Sessions for Processing</param>
        /// <returns>A ServiceBusReceiverWrapper.</returns>
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
