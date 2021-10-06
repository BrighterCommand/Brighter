using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers.ClientProvider
{
    public interface IServiceBusClientProvider
    {
        ServiceBusClient GetServiceBusClient();
        ServiceBusAdministrationClient GetServiceBusAdministrationClient();
    }
}
