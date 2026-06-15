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
using Microsoft.Extensions.Logging.Abstractions;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.ClientProvider;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers
{
    /// <summary>
    /// A wrapper for the Azure Service Bus Administration Client
    /// </summary>
    public partial class AdministrationClientWrapper : IAdministrationClientWrapper
    {
        private readonly IServiceBusClientProvider _clientProvider;
        private ServiceBusAdministrationClient _administrationClient;
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes an Instance of <see cref="AdministrationClientWrapper"/>
        /// </summary>
        /// <param name="clientProvider"></param>
        /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> used to create the logger.</param>
        public AdministrationClientWrapper(IServiceBusClientProvider clientProvider, ILoggerFactory? loggerFactory = null)
        {
            _clientProvider = clientProvider;
            _administrationClient = _clientProvider.GetServiceBusAdministrationClient();
            _logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<AdministrationClientWrapper>();
        }

        /// <summary>
        /// Reset the Connection.
        /// </summary>
        public void Reset()
        {
            Log.ResettingManagementClientWrapper(_logger);
            Initialise();
        }

        /// <summary>
        /// Create a Queue
        /// </summary>
        /// <param name="queueName">The name of the Queue</param>
        /// <param name="autoDeleteOnIdle">Number of minutes before an ideal queue will be deleted</param>
        /// <param name="maxMessageSizeInKilobytes">Ma message size in kilobytes : Only available in premium</param>
        public async Task CreateQueueAsync(string queueName, TimeSpan? autoDeleteOnIdle = null, long? maxMessageSizeInKilobytes = default)
        {
            Log.CreatingTopic(_logger, queueName);

            try
            {
                await _administrationClient.CreateQueueAsync(new CreateQueueOptions(queueName)
                {
                    AutoDeleteOnIdle = autoDeleteOnIdle ?? TimeSpan.MaxValue,
                    MaxMessageSizeInKilobytes = maxMessageSizeInKilobytes
                });
            }
            catch (Exception e)
            {
                Log.FailedToCreateQueue(_logger, e, queueName);
                throw;
            }

            Log.QueueCreated(_logger, queueName);
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
            Log.CreatingSubscriptionForTopic(_logger, subscriptionName, topicName);

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
                Log.FailedToCreateSubscriptionForTopic(_logger, e, subscriptionName, topicName);
                throw;
            }

            Log.SubscriptionForTopicCreated(_logger, subscriptionName, topicName);
        }


        /// <summary>
        /// Create a Topic
        /// Sync over async but runs in the context of creating a topic
        /// </summary>
        /// <param name="topicName">The name of the Topic</param>
        /// <param name="autoDeleteOnIdle">Number of minutes before an ideal queue will be deleted</param>
        /// <param name="maxMessageSizeInKilobytes">Ma message size in kilobytes : Only available in premium</param>
        public async Task CreateTopicAsync(string topicName, TimeSpan? autoDeleteOnIdle = null, long? maxMessageSizeInKilobytes = default)
        {
            Log.CreatingTopic(_logger, topicName);

            try
            {
                await _administrationClient.CreateTopicAsync(new CreateTopicOptions(topicName)
                {
                    AutoDeleteOnIdle = autoDeleteOnIdle ?? TimeSpan.MaxValue,
                    MaxMessageSizeInKilobytes = maxMessageSizeInKilobytes
                });
            }
            catch (Exception e)
            {
                Log.FailedToCreateTopic(_logger, e, topicName);
                throw;
            }

            Log.TopicCreated(_logger, topicName);
        }


        /// <summary>
        /// Delete a Queue
        /// </summary>
        /// <param name="queueName">The name of the Queue</param>
        public async Task DeleteQueueAsync(string queueName)
        {
            Log.DeletingQueue(_logger, queueName);
            try
            {
                    await _administrationClient.DeleteQueueAsync(queueName);
                Log.QueueSuccessfullyDeleted(_logger, queueName);
            }
            catch (Exception e)
            {
                Log.FailedToDeleteQueue(_logger, e, queueName);
            }
        }

        /// <summary>
        /// Delete a Topic
        /// </summary>
        /// <param name="topicName">The name of the Topic</param>
        public async Task DeleteTopicAsync(string topicName)
        {
            Log.DeletingTopic(_logger, topicName);
            try
            {
                await _administrationClient.DeleteTopicAsync(topicName);
                Log.TopicSuccessfullyDeleted(_logger, topicName);
            }
            catch (Exception e)
            {
                Log.FailedToDeleteTopic(_logger, e, topicName);
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
            Log.CheckingIfQueueExists(_logger, queueName);

            bool result;

            try
            {
                result = await _administrationClient.QueueExistsAsync(queueName);
            }
            catch (Exception e)
            {
                Log.FailedToCheckIfQueueExists(_logger, e, queueName);
                throw;
            }

            if (result)
            {
                Log.QueueExists(_logger, queueName);
            }
            else
            {
                Log.QueueDoesNotExist(_logger, queueName);
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
            Log.CheckingIfSubscriptionForTopicExists(_logger, subscriptionName, topicName);

            bool result;

            try
            {
                result = await _administrationClient.SubscriptionExistsAsync(topicName, subscriptionName);
            }
            catch (Exception e)
            {
                Log.FailedToCheckIfSubscriptionForTopicExists(_logger, e, subscriptionName, topicName);
                throw;
            }

            if (result)
            {
                Log.SubscriptionForTopicExists(_logger, subscriptionName, topicName);
            }
            else
            {
                Log.SubscriptionForTopicDoesNotExist(_logger, subscriptionName, topicName);
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
            Log.CheckingIfTopicExists(_logger, topicName);

            bool result;

            try
            {
                result = await _administrationClient.TopicExistsAsync(topicName);
            }
            catch (Exception e)
            {
                Log.FailedToCheckIfTopicExists(_logger, e, topicName);
                throw;
            }

            if (result)
            {
                Log.TopicExists(_logger, topicName);
            }
            else
            {
                Log.TopicDoesNotExist(_logger, topicName);
            }

            return result;
        }
        
        private void Initialise()
        {
            Log.InitialisingNewManagementClientWrapper(_logger);

            try
            {
                _administrationClient = _clientProvider.GetServiceBusAdministrationClient();
            }
            catch (Exception e)
            {
                Log.FailedToInitialiseNewManagementClientWrapper(_logger, e);
                throw;
            }

            Log.NewManagementClientWrapperInitialised(_logger);
        }

        private static partial class Log
        {
            [LoggerMessage(LogLevel.Warning, "Resetting management client wrapper...")]
            public static partial void ResettingManagementClientWrapper(ILogger logger);
            
            [LoggerMessage(LogLevel.Information, "Creating topic {Topic}...")]
            public static partial void CreatingTopic(ILogger logger, string topic);
            
            [LoggerMessage(LogLevel.Error, "Failed to create queue {Queue}.")]
            public static partial void FailedToCreateQueue(ILogger logger, Exception exception, string queue);
            
            [LoggerMessage(LogLevel.Information, "Queue {Queue} created.")]
            public static partial void QueueCreated(ILogger logger, string queue);
            
            [LoggerMessage(LogLevel.Information, "Creating subscription {ChannelName} for topic {Topic}...")]
            public static partial void CreatingSubscriptionForTopic(ILogger logger, string channelName, string topic);

            [LoggerMessage(LogLevel.Error, "Failed to create subscription {ChannelName} for topic {Topic}.")]
            public static partial void FailedToCreateSubscriptionForTopic(ILogger logger, Exception exception, string channelName, string topic);
            
            [LoggerMessage(LogLevel.Information, "Subscription {ChannelName} for topic {Topic} created.")]
            public static partial void SubscriptionForTopicCreated(ILogger logger, string channelName, string topic);
            
            [LoggerMessage(LogLevel.Error, "Failed to create topic {Topic}.")]
            public static partial void FailedToCreateTopic(ILogger logger, Exception exception, string topic);
            
            [LoggerMessage(LogLevel.Information, "Topic {Topic} created.")]
            public static partial void TopicCreated(ILogger logger, string topic);
            
            [LoggerMessage(LogLevel.Information, "Deleting queue {Queue}...")]
            public static partial void DeletingQueue(ILogger logger, string queue);
            
            [LoggerMessage(LogLevel.Information, "Queue {Queue} successfully deleted")]
            public static partial void QueueSuccessfullyDeleted(ILogger logger, string queue);
            
            [LoggerMessage(LogLevel.Error, "Failed to delete Queue {Queue}")]
            public static partial void FailedToDeleteQueue(ILogger logger, Exception exception, string queue);
            
            [LoggerMessage(LogLevel.Information, "Deleting topic {Topic}...")]
            public static partial void DeletingTopic(ILogger logger, string topic);
            
            [LoggerMessage(LogLevel.Information, "Topic {Topic} successfully deleted")]
            public static partial void TopicSuccessfullyDeleted(ILogger logger, string topic);
            
            [LoggerMessage(LogLevel.Error, "Failed to delete Topic {Topic}")]
            public static partial void FailedToDeleteTopic(ILogger logger, Exception exception, string topic);
            
            [LoggerMessage(LogLevel.Debug, "Checking if queue {Queue} exists...")]
            public static partial void CheckingIfQueueExists(ILogger logger, string queue);
            
            [LoggerMessage(LogLevel.Error, "Failed to check if queue {Queue} exists")]
            public static partial void FailedToCheckIfQueueExists(ILogger logger, Exception exception, string queue);
            
            [LoggerMessage(LogLevel.Debug, "Queue {Queue} exists")]
            public static partial void QueueExists(ILogger logger, string queue);
            
            [LoggerMessage(LogLevel.Warning, "Queue {Queue} does not exist")]
            public static partial void QueueDoesNotExist(ILogger logger, string queue);
            
            [LoggerMessage(LogLevel.Debug, "Checking if subscription {ChannelName} for topic {Topic} exists...")]
            public static partial void CheckingIfSubscriptionForTopicExists(ILogger logger, string channelName, string topic);
            
            [LoggerMessage(LogLevel.Error, "Failed to check if subscription {ChannelName} for topic {Topic} exists.")]
            public static partial void FailedToCheckIfSubscriptionForTopicExists(ILogger logger, Exception exception, string channelName, string topic);
            
            [LoggerMessage(LogLevel.Debug, "Subscription {ChannelName} for topic {Topic} exists.")]
            public static partial void SubscriptionForTopicExists(ILogger logger, string channelName, string topic);
            
            [LoggerMessage(LogLevel.Warning, "Subscription {ChannelName} for topic {Topic} does not exist.")]
            public static partial void SubscriptionForTopicDoesNotExist(ILogger logger, string channelName, string topic);
            
            [LoggerMessage(LogLevel.Debug, "Checking if topic {Topic} exists...")]
            public static partial void CheckingIfTopicExists(ILogger logger, string topic);
            
            [LoggerMessage(LogLevel.Error, "Failed to check if topic {Topic} exists")]
            public static partial void FailedToCheckIfTopicExists(ILogger logger, Exception exception, string topic);
            
            [LoggerMessage(LogLevel.Debug, "Topic {Topic} exists")]
            public static partial void TopicExists(ILogger logger, string topic);
            
            [LoggerMessage(LogLevel.Warning, "Topic {Topic} does not exist")]
            public static partial void TopicDoesNotExist(ILogger logger, string topic);
            
            [LoggerMessage(LogLevel.Debug, "Initialising new management client wrapper...")]
            public static partial void InitialisingNewManagementClientWrapper(ILogger logger);
            
            [LoggerMessage(LogLevel.Error, "Failed to initialise new management client wrapper.")]
            public static partial void FailedToInitialiseNewManagementClientWrapper(ILogger logger, Exception exception);
            
            [LoggerMessage(LogLevel.Debug, "New management client wrapper initialised.")]
            public static partial void NewManagementClientWrapperInitialised(ILogger logger);
        }
    }
}

