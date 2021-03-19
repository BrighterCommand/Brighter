using System;
using Microsoft.Azure.ServiceBus.Management;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers
{
    public class ManagementClientWrapper : IManagementClientWrapper
    {
        private ManagementClient _managementClient;
        private static readonly Lazy<ILog> Logger = new Lazy<ILog>(LogProvider.For<ManagementClientWrapper>);
        private readonly AzureServiceBusConfiguration _configuration;
        
        public ManagementClientWrapper(AzureServiceBusConfiguration configuration)
        {
            _configuration = configuration;
            Initialise();
        }

        private void Initialise()
        {
            Logger.Value.Debug("Initialising new management client wrapper...");
            
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
                Logger.Value.ErrorException("Failed to initialise new management client wrapper.", e);
                throw;
            }

            Logger.Value.Debug("New management client wrapper initialised.");
        }

        public void Reset()
        {
            Logger.Value.Warn("Resetting management client wrapper...");
            Initialise();
        }

        public bool TopicExists(string topic)
        {
            Logger.Value.Debug($"Checking if topic {topic} exists...");
            
            bool result;

            try
            {
                result = _managementClient.TopicExistsAsync(topic).GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                Logger.Value.ErrorException($"Failed to check if topic {topic} exists.", e);
                throw;
            }

            if (result)
            {
                Logger.Value.Debug($"Topic {topic} exists.");
            }
            else
            {
                Logger.Value.Warn($"Topic {topic} does not exist.");
            }
            
            return result;
        }

        public void CreateTopic(string topic)
        {
            Logger.Value.Info($"Creating topic {topic}...");

            try
            {
                _managementClient.CreateTopicAsync(topic).GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                Logger.Value.ErrorException($"Failed to create topic {topic}.", e);
                throw;
            }
            
            Logger.Value.Info($"Topic {topic} created.");
        }

        public bool SubscriptionExists(string topicName, string subscriptionName)
        {
            Logger.Value.Debug($"Checking if subscription {subscriptionName} for topic {topicName} exists...");

            bool result;

            try
            {
                result =_managementClient.SubscriptionExistsAsync(topicName, subscriptionName).Result;
            }
            catch (Exception e)
            {
                Logger.Value.ErrorException($"Failed to check if subscription {subscriptionName} for topic {topicName} exists.", e);
                throw;
            }
            
            if (result)
            {
                Logger.Value.Debug($"Subscription {subscriptionName} for topic {topicName} exists.");
            }
            else
            {
                Logger.Value.Warn($"Subscription {subscriptionName} for topic {topicName} does not exist.");
            }

            return result;
        }

        public void CreateSubscription(string topicName, string subscriptionName, int maxDeliveryCount = 2000)
        {
            Logger.Value.Info($"Creating subscription {subscriptionName} for topic {topicName}...");

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
                Logger.Value.ErrorException($"Failed to create subscription {subscriptionName} for topic {topicName}.", e);
                throw;
            }
            
            Logger.Value.Info($"Subscription {subscriptionName} for topic {topicName} created.");
        }
    }
}
