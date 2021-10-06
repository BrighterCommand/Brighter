using Azure.Messaging.ServiceBus;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers
{
    public class ServiceBusReceiverProvider : IServiceBusReceiverProvider
    {
        private readonly ServiceBusClient _client;

        public ServiceBusReceiverProvider(ClientProvider.IServiceBusClientProvider clientProvider)
        {
            _client = clientProvider.GetServiceBusClient();
        }

        public IServiceBusReceiverWrapper Get(string topicName, string subscriptionName, ServiceBusReceiveMode receiveMode)
        {
            var messageReceiver = _client.CreateReceiver(topicName, subscriptionName,
                new ServiceBusReceiverOptions() {ReceiveMode = receiveMode,});
            return new ServiceBusReceiverWrapper(messageReceiver);
        }
    }
}
