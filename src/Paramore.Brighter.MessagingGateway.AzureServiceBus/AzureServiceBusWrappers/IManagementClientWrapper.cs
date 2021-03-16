namespace Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers
{
    public interface IManagementClientWrapper
    {
        bool TopicExists(string topic);

        void CreateTopic(string topic);

        bool SubscriptionExists(string topicName, string subscriptionName);

        void CreateSubscription(string topicName, string subscriptionName, int maxDeliveryCount);

        void Reset();
    }
}
