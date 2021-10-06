using System.Threading.Tasks;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers
{
    public interface IAdministrationClientWrapper
    {
        bool TopicExists(string topic);

        void CreateTopic(string topic);

        Task DeleteTopicAsync(string topic);

        bool SubscriptionExists(string topicName, string subscriptionName);

        void CreateSubscription(string topicName, string subscriptionName, int maxDeliveryCount);

        void Reset();
    }
}
