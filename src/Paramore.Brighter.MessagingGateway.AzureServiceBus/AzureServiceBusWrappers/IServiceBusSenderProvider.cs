namespace Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers
{
    public interface IServiceBusSenderProvider
    {
        IServiceBusSenderWrapper Get(string topic);
    }
}
