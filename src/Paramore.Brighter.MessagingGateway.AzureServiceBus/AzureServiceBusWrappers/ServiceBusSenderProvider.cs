using Azure.Messaging.ServiceBus;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.ClientProvider;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers
{
    internal sealed class ServiceBusSenderProvider : IServiceBusSenderProvider
    {
        private readonly ServiceBusClient _client;
        private readonly ServiceBusSenderOptions? _options;

        public ServiceBusSenderProvider(IServiceBusClientProvider clientProvider, ServiceBusSenderOptions? options = null)
        {
            _client = clientProvider.GetServiceBusClient();
            _options = options;
        }

        public IServiceBusSenderWrapper Get(string topicOrQueueName)
        {
            return new ServiceBusSenderWrapper(_client.CreateSender(topicOrQueueName, _options));
        }
    }
}
