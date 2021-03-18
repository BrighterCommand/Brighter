namespace Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers
{
    public interface ITopicClientProvider
    {
        ITopicClient Get(string topic);
    }
}
