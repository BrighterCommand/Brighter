using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.ClientProvider;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers
{
    /// <summary>
    /// A wrapper for the Azure Service Bus Administration Client
    /// </summary>
    public class AdministrationClientWrapper : IAdministrationClientWrapper
    {
        private readonly IServiceBusClientProvider _clientProvider;
        private ServiceBusAdministrationClient _administrationClient;
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<AdministrationClientWrapper>();

        /// <summary>
        /// Initializes an Instance of <see cref="AdministrationClientWrapper"/>
        /// </summary>
        /// <param name="clientProvider"></param>
        public AdministrationClientWrapper(IServiceBusClientProvider clientProvider)
        {
            _clientProvider = clientProvider;
            Initialise();
        }

        /// <summary>
        /// Reset the Connection.
        /// </summary>
        public void Reset()
        {
            s_logger.LogWarning("Resetting management client wrapper...");
            Initialise();
        }

        /// <summary>
        /// Check if a Topic exists
        /// </summary>
        /// <param name="topicOrQueue">The name of the Topic or Queue.</param>
        /// <param name="useQueue">Use a Queue instead of a Topic</param>
        /// <returns>True if the Channel exists.</returns>
        public bool TopicOrQueueExists(string topicOrQueue, bool useQueue)
        {
            s_logger.LogDebug("Checking if topic {Topic} exists...", topicOrQueue);

            bool result;

            try
            {
                result = useQueue ? _administrationClient.QueueExistsAsync(topicOrQueue).GetAwaiter().GetResult(): 
                _administrationClient.TopicExistsAsync(topicOrQueue).GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                s_logger.LogError(e,"Failed to check if topic {Topic} exists.", topicOrQueue);
                throw;
            }

            if (result)
            {
                s_logger.LogDebug("Topic {Topic} exists.", topicOrQueue);
            }
            else
            {
                s_logger.LogWarning("Topic {Topic} does not exist.", topicOrQueue);
            }

            return result;
        }

        /// <summary>
        /// Create a Topic
        /// </summary>
        /// <param name="topicOrQueue"></param>
        /// <param name="useQueues"></param>
        /// <param name="autoDeleteOnIdle">Number of minutes before an ideal queue will be deleted</param>
        public void CreateChannel(string topicOrQueue, bool useQueues, TimeSpan? autoDeleteOnIdle = null)
        {
            s_logger.LogInformation("Creating topic {Topic}...", topicOrQueue);

            try
            {
                if(useQueues)
                    _administrationClient.CreateQueueAsync(new CreateQueueOptions(topicOrQueue){
                        AutoDeleteOnIdle = autoDeleteOnIdle ?? TimeSpan.MaxValue
                    }).GetAwaiter().GetResult();
                else
                    _administrationClient.CreateTopicAsync(new CreateTopicOptions(topicOrQueue)
                {
                    AutoDeleteOnIdle = autoDeleteOnIdle ?? TimeSpan.MaxValue
                }).GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                s_logger.LogError(e,"Failed to create topic {Topic}.", topicOrQueue);
                throw;
            }

            s_logger.LogInformation("Topic {Topic} created.", topicOrQueue);
        }

        /// <summary>
        /// Delete a Topic.
        /// </summary>
        /// <param name="topicOrQueue"></param>
        /// <param name="useQueues"></param>
        public async Task DeleteChannelAsync(string topicOrQueue, bool useQueues)
        {
            s_logger.LogInformation("Deleting topic {Topic}...", topicOrQueue);
            try
            {
                if(useQueues)
                    await _administrationClient.DeleteQueueAsync(topicOrQueue);
                else
                    await _administrationClient.DeleteTopicAsync(topicOrQueue);
                s_logger.LogInformation("Topic {Topic} successfully deleted", topicOrQueue);
            }
            catch (Exception e)
            {
                s_logger.LogError(e,"Failed to delete Topic {Topic}", topicOrQueue);
            }
        }

        /// <summary>
        /// Check if a Subscription Exists for a Topic.
        /// </summary>
        /// <param name="topicName">The name of the Topic.</param>
        /// <param name="subscriptionName">The name of the Subscription</param>
        /// <returns>True if the subscription exists on the specified Topic.</returns>
        public bool SubscriptionExists(string topicName, string subscriptionName)
        {
            s_logger.LogDebug("Checking if subscription {ChannelName} for topic {Topic} exists...", subscriptionName, topicName);

            bool result;

            try
            {
                result =_administrationClient.SubscriptionExistsAsync(topicName, subscriptionName).Result;
            }
            catch (Exception e)
            {
                s_logger.LogError(e, "Failed to check if subscription {ChannelName} for topic {Topic} exists.", subscriptionName, topicName);
                throw;
            }

            if (result)
            {
                s_logger.LogDebug("Subscription {ChannelName} for topic {Topic} exists.", subscriptionName, topicName);
            }
            else
            {
                s_logger.LogWarning("Subscription {ChannelName} for topic {Topic} does not exist.", subscriptionName, topicName);
            }

            return result;
        }

        /// <summary>
        /// Create a Subscription.
        /// </summary>
        /// <param name="topicName">The name of the Topic.</param>
        /// <param name="subscriptionName">The name of the Subscription.</param>
        /// <param name="subscriptionConfiguration">The configuration options for the subscriptions.</param>
        public void CreateSubscription(string topicName, string subscriptionName, AzureServiceBusSubscriptionConfiguration subscriptionConfiguration)
        {
            CreateSubscriptionAsync(topicName, subscriptionName, subscriptionConfiguration).Wait();
        }

        /// <summary>
        /// Get a Subscription.
        /// </summary>
        /// <param name="topicName">The name of the Topic.</param>
        /// <param name="subscriptionName">The name of the Subscription.</param>
        /// <param name="cancellationToken">The Cancellation Token.</param>
        public async Task<SubscriptionProperties> GetSubscriptionAsync(string topicName, string subscriptionName,
            CancellationToken cancellationToken = default)
        {
            return await _administrationClient.GetSubscriptionAsync(topicName, subscriptionName, cancellationToken);
        }

        private void Initialise()
        {
            s_logger.LogDebug("Initialising new management client wrapper...");

            try
            {
                _administrationClient = _clientProvider.GetServiceBusAdministrationClient();
            }
            catch (Exception e)
            {
                s_logger.LogError(e,"Failed to initialise new management client wrapper.");
                throw;
            }

            s_logger.LogDebug("New management client wrapper initialised.");
        }

        private async Task CreateSubscriptionAsync(string topicName, string subscriptionName, AzureServiceBusSubscriptionConfiguration subscriptionConfiguration)
        {
            s_logger.LogInformation("Creating subscription {ChannelName} for topic {Topic}...", subscriptionName, topicName);

            if (!TopicOrQueueExists(topicName, false))
            {
                CreateChannel(topicName, false, subscriptionConfiguration.QueueIdleBeforeDelete);
            }

            var subscriptionOptions = new CreateSubscriptionOptions(topicName, subscriptionName)
            {
                MaxDeliveryCount = subscriptionConfiguration.MaxDeliveryCount,
                DeadLetteringOnMessageExpiration = subscriptionConfiguration.DeadLetteringOnMessageExpiration,
                LockDuration = subscriptionConfiguration.LockDuration,
                DefaultMessageTimeToLive = subscriptionConfiguration.DefaultMessageTimeToLive,
                AutoDeleteOnIdle = subscriptionConfiguration.QueueIdleBeforeDelete,
                RequiresSession = subscriptionConfiguration.RequireSession
            };

            var ruleOptions = string.IsNullOrEmpty(subscriptionConfiguration.SqlFilter)
                ? new CreateRuleOptions() : new CreateRuleOptions("sqlFilter",new SqlRuleFilter(subscriptionConfiguration.SqlFilter));

            try
            {
                await _administrationClient.CreateSubscriptionAsync(subscriptionOptions, ruleOptions);
            }
            catch (Exception e)
            {
                s_logger.LogError(e, "Failed to create subscription {ChannelName} for topic {Topic}.", subscriptionName, topicName);
                throw;
            }

            s_logger.LogInformation("Subscription {ChannelName} for topic {Topic} created.", subscriptionName, topicName);
        }
    }
}
