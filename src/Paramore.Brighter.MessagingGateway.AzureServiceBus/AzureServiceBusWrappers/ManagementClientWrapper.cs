using System;
using Microsoft.Azure.ServiceBus.Management;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers
{
    public class ManagementClientWrapper : IManagementClientWrapper
    {
        private ManagementClient _managementClient;
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<ManagementClientWrapper>();
        private readonly AzureServiceBusConfiguration _configuration;
        
        public ManagementClientWrapper(AzureServiceBusConfiguration configuration)
        {
            _configuration = configuration;
            Initialise();
        }

        private void Initialise()
        {
            s_logger.LogDebug("Initialising new management client wrapper...");
            
            try
            {
                if (_configuration == null)
                {
                    throw new ArgumentNullException(nameof(_configuration), "Configuration is null, ensure this is set in the constructor.");
                }

                _managementClient = new ManagementClient(_configuration.ConnectionString);
            }
            catch (Exception e)
            {
                s_logger.LogError(e,"Failed to initialise new management client wrapper.");
                throw;
            }

            s_logger.LogDebug("New management client wrapper initialised.");
        }

        public void Reset()
        {
            s_logger.LogWarning("Resetting management client wrapper...");
            Initialise();
        }

        public bool TopicExists(string topic)
        {
            s_logger.LogDebug("Checking if topic {Topic} exists...", topic);
            
            bool result;

            try
            {
                result = _managementClient.TopicExistsAsync(topic).GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                s_logger.LogError(e,"Failed to check if topic {Topic} exists.", topic);
                throw;
            }

            if (result)
            {
                s_logger.LogDebug("Topic {Topic} exists.", topic);
            }
            else
            {
                s_logger.LogWarning("Topic {Topic} does not exist.", topic);
            }
            
            return result;
        }

        public void CreateTopic(string topic)
        {
            s_logger.LogInformation("Creating topic {Topic}...", topic);

            try
            {
                _managementClient.CreateTopicAsync(topic).GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                s_logger.LogError(e,"Failed to create topic {Topic}.", topic);
                throw;
            }
            
            s_logger.LogInformation("Topic {Topic} created.", topic);
        }

        public bool SubscriptionExists(string topicName, string subscriptionName)
        {
            s_logger.LogDebug("Checking if subscription {ChannelName} for topic {Topic} exists...", subscriptionName, topicName);

            bool result;

            try
            {
                result =_managementClient.SubscriptionExistsAsync(topicName, subscriptionName).Result;
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

        public void CreateSubscription(string topicName, string subscriptionName, int maxDeliveryCount = 2000)
        {
            s_logger.LogInformation("Creating subscription {ChannelName} for topic {Topic}...", subscriptionName, topicName);

            if (!TopicExists(topicName))
            {
                CreateTopic(topicName);
            }

            var subscriptionDescription = new SubscriptionDescription(topicName, subscriptionName)
            {
                MaxDeliveryCount = maxDeliveryCount
            };

            try
            {
                _managementClient.CreateSubscriptionAsync(subscriptionDescription).Wait();
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
