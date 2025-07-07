#region Licence
/* The MIT License (MIT)
Copyright © 2024 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */
#endregion

using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.ClientProvider;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers
{
    internal sealed class ServiceBusReceiverProvider(IServiceBusClientProvider clientProvider) : IServiceBusReceiverProvider
    {
        private readonly ServiceBusClient _client = clientProvider.GetServiceBusClient();

        /// <summary>
        /// Gets a <see cref="IServiceBusReceiverWrapper"/> for a Service Bus Queue
        /// Sync over async used here, alright in the context of receiver creation
        /// </summary>
        /// <param name="queueName">The name of the Topic.</param>
        /// <param name="sessionEnabled">Use Sessions for Processing</param>
        /// <returns>A ServiceBusReceiverWrapper.</returns>
        public async Task<IServiceBusReceiverWrapper?> GetAsync(string queueName, bool sessionEnabled)
        {
            if (sessionEnabled)
            {
                try
                {
                    return new ServiceBusReceiverWrapper(await _client.AcceptNextSessionAsync(queueName,
                        new ServiceBusSessionReceiverOptions() {ReceiveMode = ServiceBusReceiveMode.PeekLock}));
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
                    new ServiceBusReceiverOptions { ReceiveMode = ServiceBusReceiveMode.PeekLock }));
            }
        }

        /// <summary>
        /// Gets a <see cref="IServiceBusReceiverWrapper"/> for a Service Bus Topic
        /// Sync over async used here, alright in the context of receiver creation
        /// </summary>
        /// <param name="topicName">The name of the Topic.</param>
        /// <param name="subscriptionName">The name of the Subscription on the Topic.</param>
        /// <param name="sessionEnabled">Use Sessions for Processing</param>
        /// <returns>A ServiceBusReceiverWrapper.</returns>
        public async Task<IServiceBusReceiverWrapper?> GetAsync(string topicName, string subscriptionName, bool sessionEnabled)
        {
            if (sessionEnabled)
            {
                try
                {
                    return new ServiceBusReceiverWrapper(await _client.AcceptNextSessionAsync(topicName, subscriptionName,
                        new ServiceBusSessionReceiverOptions() {ReceiveMode = ServiceBusReceiveMode.PeekLock}));
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
                    new ServiceBusReceiverOptions { ReceiveMode = ServiceBusReceiveMode.PeekLock }));
            }
        }
    }
}
