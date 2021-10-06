using Azure.Messaging.ServiceBus;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers
{
    public class ServiceBusSenderProvider : IServiceBusSenderProvider
    {
        private readonly ServiceBusClient _client;

        public ServiceBusSenderProvider(ClientProvider.IServiceBusClientProvider clientProvider)
        {
            _client = clientProvider.GetServiceBusClient();
        }

        public IServiceBusSenderWrapper Get(string topic)
        {
            return new ServiceBusSenderWrapper(_client.CreateSender(topic));
        }
    }
}
