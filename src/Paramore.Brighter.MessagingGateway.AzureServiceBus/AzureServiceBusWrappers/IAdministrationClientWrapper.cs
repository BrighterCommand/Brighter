using System.Threading.Tasks;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers
{
    /// <summary>
    /// A wrapper for the Azure Service Bus Administration Client
    /// </summary>
    public interface IAdministrationClientWrapper
    {
        /// <summary>
        /// Check if a Topic exists
        /// </summary>
        /// <param name="topic">The name of the Topic.</param>
        /// <returns>True if the Topic exists.</returns>
        bool TopicExists(string topic);

        /// /// <summary>
        /// Create a Topic
        /// </summary>
        /// <param name="topic">The name of the Topic</param>
        void CreateTopic(string topic);

        /// <summary>
        /// Delete a Topic.
        /// </summary>
        /// <param name="topic">The name of the Topic.</param>
        Task DeleteTopicAsync(string topic);

        /// <summary>
        /// Check if a Subscription Exists for a Topic.
        /// </summary>
        /// <param name="topicName">The name of the Topic.</param>
        /// <param name="subscriptionName">The name of the Subscription</param>
        /// <returns>True if the subscription exists on the specified Topic.</returns>
        bool SubscriptionExists(string topicName, string subscriptionName);

        /// <summary>
        /// Create a Subscription.
        /// </summary>
        /// <param name="topicName">The name of the Topic.</param>
        /// <param name="subscriptionName">The name of the Subscription.</param>
        /// <param name="maxDeliveryCount">Maximum message delivery count.</param>
        void CreateSubscription(string topicName, string subscriptionName, int maxDeliveryCount);

        /// <summary>
        /// Reset the Connection.
        /// </summary>
        void Reset();
    }
}
