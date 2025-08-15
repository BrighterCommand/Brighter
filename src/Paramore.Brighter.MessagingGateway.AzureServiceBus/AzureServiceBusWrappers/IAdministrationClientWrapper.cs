using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus.Administration;

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
        /// <param name="topicName">The name of the Topic.</param>
        /// <returns>True if the Topic exists.</returns>
        Task<bool> TopicExistsAsync(string topicName);
        
        /// <summary>
        /// Check if a Queue exists
        /// </summary>
        /// <param name="queueName">The name of the Queue.</param>
        /// <returns>True if the Queue exists.</returns>
        Task<bool> QueueExistsAsync(string queueName);

        /// <summary>
        /// Create a Queue
        /// </summary>
        /// <param name="queueName">The name of the Queue</param>
        /// <param name="autoDeleteOnIdle">Number of minutes before an ideal queue will be deleted</param>
        /// <param name="maxMessageSizeInKilobytes">Ma message size in kilobytes : Only available in premium</param>
        Task CreateQueueAsync(string queueName, TimeSpan? autoDeleteOnIdle = null, long? maxMessageSizeInKilobytes = default);

        /// <summary>
        /// Create a Topic
        /// </summary>
        /// <param name="topicName">The name of the Topic</param>
        /// <param name="autoDeleteOnIdle">Number of minutes before an ideal queue will be deleted</param>
        /// <param name="maxMessageSizeInKilobytes">Ma message size in kilobytes : Only available in premium</param>
        Task CreateTopicAsync(string topicName, TimeSpan? autoDeleteOnIdle = null, long? maxMessageSizeInKilobytes = default);

        /// <summary>
        /// Delete a Queue
        /// </summary>
        /// <param name="queueName">The name of the Queue</param>
        Task DeleteQueueAsync(string queueName);

        /// <summary>
        /// Delete a Topic
        /// </summary>
        /// <param name="topicName">The name of the Topic</param>
        Task DeleteTopicAsync(string topicName);

        /// <summary>
        /// Check if a Subscription Exists for a Topic.
        /// </summary>
        /// <param name="topicName">The name of the Topic.</param>
        /// <param name="subscriptionName">The name of the Subscription</param>
        /// <returns>True if the subscription exists on the specified Topic.</returns>
        Task<bool> SubscriptionExistsAsync(string topicName, string subscriptionName);

        /// <summary>
        /// Create a Subscription.
        /// </summary>
        /// <param name="topicName">The name of the Topic.</param>
        /// <param name="subscriptionName">The name of the Subscription.</param>
        /// <param name="subscriptionConfiguration">The configuration options for the subscriptions.</param>
        Task CreateSubscriptionAsync(string topicName, string subscriptionName, AzureServiceBusSubscriptionConfiguration subscriptionConfiguration);

        /// <summary>
        /// Reset the Connection.
        /// </summary>
        void Reset();

        /// <summary>
        /// GetAsync a Subscription.
        /// </summary>
        /// <param name="topicName">The name of the Topic.</param>
        /// <param name="subscriptionName">The name of the Subscription.</param>
        /// <param name="cancellationToken">The Cancellation Token.</param>
        Task<SubscriptionProperties> GetSubscriptionAsync(string topicName, string subscriptionName,
            CancellationToken cancellationToken = default);
    }
}
