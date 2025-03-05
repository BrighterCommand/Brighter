using Azure.Messaging.ServiceBus;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.ClientProvider;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers
{
    internal sealed class ServiceBusSenderProvider : IServiceBusSenderProvider
    {
        private readonly ServiceBusClient _client;

        public ServiceBusSenderProvider(IServiceBusClientProvider clientProvider)
        {
            _client = clientProvider.GetServiceBusClient();
        }

        public IServiceBusSenderWrapper Get(string topicOrQueueName)
        {
            return new ServiceBusSenderWrapper(_client.CreateSender(topicOrQueueName));
        }
    }
}
