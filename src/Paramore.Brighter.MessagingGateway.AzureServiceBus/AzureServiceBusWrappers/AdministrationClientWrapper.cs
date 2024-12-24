#region Licence
/* The MIT License (MIT)
Copyright © 2024 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */
#endregion

using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.ClientProvider;
using Paramore.Brighter.Tasks;

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
            _administrationClient = _clientProvider.GetServiceBusAdministrationClient();
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
        /// Create a Queue
        /// Sync over async but alright in the context of creating a queue
        /// </summary>
        /// <param name="queueName">The name of the Queue</param>
        /// <param name="autoDeleteOnIdle">Number of minutes before an ideal queue will be deleted</param>
        public async Task CreateQueueAsync(string queueName, TimeSpan? autoDeleteOnIdle = null)
        {
            s_logger.LogInformation("Creating topic {Topic}...", queueName);

            try
            {
                await _administrationClient.CreateQueueAsync(new CreateQueueOptions(queueName)
                {
                    AutoDeleteOnIdle = autoDeleteOnIdle ?? TimeSpan.MaxValue
                });
            }
            catch (Exception e)
            {
                s_logger.LogError(e, "Failed to create queue {Queue}.", queueName);
                throw;
            }

            s_logger.LogInformation("Queue {Queue} created.", queueName);
        }
        
        /// <summary>
        /// Create a Subscription.
        /// Sync over Async but alright in the context of creating a subscription
        /// </summary>
        /// <param name="topicName">The name of the Topic.</param>
        /// <param name="subscriptionName">The name of the Subscription.</param>
        /// <param name="subscriptionConfiguration">The configuration options for the subscriptions.</param>
        public async Task CreateSubscriptionAsync(string topicName, string subscriptionName, AzureServiceBusSubscriptionConfiguration subscriptionConfiguration)
        {
            s_logger.LogInformation("Creating subscription {ChannelName} for topic {Topic}...", subscriptionName, topicName);

            if (!await TopicExistsAsync(topicName))
            {
                await CreateTopicAsync(topicName, subscriptionConfiguration.QueueIdleBeforeDelete);
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

        
        /// <summary>
        /// Create a Topic
        /// Sync over async but runs in the context of creating a topic
        /// </summary>
        /// <param name="topicName">The name of the Topic</param>
        /// <param name="autoDeleteOnIdle">Number of minutes before an ideal queue will be deleted</param>
        public async Task CreateTopicAsync(string topicName, TimeSpan? autoDeleteOnIdle = null)
        {
            s_logger.LogInformation("Creating topic {Topic}...", topicName);

            try
            {
                await _administrationClient.CreateTopicAsync(new CreateTopicOptions(topicName)
                {
                    AutoDeleteOnIdle = autoDeleteOnIdle ?? TimeSpan.MaxValue
                });
            }
            catch (Exception e)
            {
                s_logger.LogError(e, "Failed to create topic {Topic}.", topicName);
                throw;
            }

            s_logger.LogInformation("Topic {Topic} created.", topicName);
        }


        /// <summary>
        /// Delete a Queue
        /// </summary>
        /// <param name="queueName">The name of the Queue</param>
        public async Task DeleteQueueAsync(string queueName)
        {
            s_logger.LogInformation("Deleting queue {Queue}...", queueName);
            try
            {
                    await _administrationClient.DeleteQueueAsync(queueName);
                s_logger.LogInformation("Queue {Queue} successfully deleted", queueName);
            }
            catch (Exception e)
            {
                s_logger.LogError(e,"Failed to delete Queue {Queue}", queueName);
            }
        }

        /// <summary>
        /// Delete a Topic
        /// </summary>
        /// <param name="topicName">The name of the Topic</param>
        public async Task DeleteTopicAsync(string topicName)
        {
            s_logger.LogInformation("Deleting topic {Topic}...", topicName);
            try
            {
                await _administrationClient.DeleteTopicAsync(topicName);
                s_logger.LogInformation("Topic {Topic} successfully deleted", topicName);
            }
            catch (Exception e)
            {
                s_logger.LogError(e, "Failed to delete Topic {Topic}", topicName);
            }
        }
        
        /// <summary>
        /// GetAsync a Subscription.
        /// </summary>
        /// <param name="topicName">The name of the Topic.</param>
        /// <param name="subscriptionName">The name of the Subscription.</param>
        /// <param name="cancellationToken">The Cancellation Token.</param>
        public async Task<SubscriptionProperties> GetSubscriptionAsync(string topicName, string subscriptionName,
            CancellationToken cancellationToken = default)
        {
            return await _administrationClient.GetSubscriptionAsync(topicName, subscriptionName, cancellationToken);
        }
        
        /// <summary>
        /// Check if a Queue exists
        /// Sync over async but runs in the context of checking queue existence
        /// </summary>
        /// <param name="queueName">The name of the Queue.</param>
        /// <returns>True if the Queue exists.</returns>
        public async Task<bool> QueueExistsAsync(string queueName)
        {
            s_logger.LogDebug("Checking if queue {Queue} exists...", queueName);

            bool result;

            try
            {
                result = await _administrationClient.QueueExistsAsync(queueName);
            }
            catch (Exception e)
            {
                s_logger.LogError(e,"Failed to check if queue {Queue} exists", queueName);
                throw;
            }

            if (result)
            {
                s_logger.LogDebug("Queue {Queue} exists", queueName);
            }
            else
            {
                s_logger.LogWarning("Queue {Queue} does not exist", queueName);
            }

            return result;
        }

        /// <summary>
        /// Check if a Subscription Exists for a Topic.
        /// </summary>
        /// <param name="topicName">The name of the Topic.</param>
        /// <param name="subscriptionName">The name of the Subscription</param>
        /// <returns>True if the subscription exists on the specified Topic.</returns>
        public async Task<bool> SubscriptionExistsAsync(string topicName, string subscriptionName)
        {
            s_logger.LogDebug("Checking if subscription {ChannelName} for topic {Topic} exists...", subscriptionName, topicName);

            bool result;

            try
            {
                result = await _administrationClient.SubscriptionExistsAsync(topicName, subscriptionName);
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
        /// Check if a Topic exists
        /// Sync over async but alright in the context of checking topic existence
        /// </summary>
        /// <param name="topicName">The name of the Topic.</param>
        /// <returns>True if the Topic exists.</returns>
        public async Task<bool> TopicExistsAsync(string topicName)
        {
            s_logger.LogDebug("Checking if topic {Topic} exists...", topicName);

            bool result;

            try
            {
                result = await _administrationClient.TopicExistsAsync(topicName);
            }
            catch (Exception e)
            {
                s_logger.LogError(e,"Failed to check if topic {Topic} exists", topicName);
                throw;
            }

            if (result)
            {
                s_logger.LogDebug("Topic {Topic} exists", topicName);
            }
            else
            {
                s_logger.LogWarning("Topic {Topic} does not exist", topicName);
            }

            return result;
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
    }
}
