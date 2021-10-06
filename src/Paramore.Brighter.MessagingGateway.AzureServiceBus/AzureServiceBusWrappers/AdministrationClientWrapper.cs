using System;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers
{
    public class AdministrationClientWrapper : IAdministrationClientWrapper
    {
        private readonly ClientProvider.IServiceBusClientProvider _clientProvider;
        private ServiceBusAdministrationClient _administrationClient;
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<AdministrationClientWrapper>();

        public AdministrationClientWrapper(ClientProvider.IServiceBusClientProvider clientProvider)
        {
            _clientProvider = clientProvider;
            Initialise();
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
                result = _administrationClient.TopicExistsAsync(topic).GetAwaiter().GetResult();
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
                _administrationClient.CreateTopicAsync(topic).GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                s_logger.LogError(e,"Failed to create topic {Topic}.", topic);
                throw;
            }
            
            s_logger.LogInformation("Topic {Topic} created.", topic);
        }

        public async Task DeleteTopicAsync(string topic)
        {
            s_logger.LogInformation("Deleting topic {Topic}...", topic);
            try
            {
                await _administrationClient.DeleteTopicAsync(topic);
                s_logger.LogInformation("Topic {Topic} successfully deleted", topic);
            }
            catch (Exception e)
            {
                s_logger.LogError(e,"Failed to delete Topic {Topic}", topic);
            }
        }

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

        public void CreateSubscription(string topicName, string subscriptionName, int maxDeliveryCount = 2000)
        {
            CreateSubscriptionAsync(topicName, subscriptionName, maxDeliveryCount).Wait();
        }
        
        private async Task CreateSubscriptionAsync(string topicName, string subscriptionName, int maxDeliveryCount = 2000)
        {
            s_logger.LogInformation("Creating subscription {ChannelName} for topic {Topic}...", subscriptionName, topicName);

            if (!TopicExists(topicName))
            {
                CreateTopic(topicName);
            }

            var subscriptionOptions = new CreateSubscriptionOptions(topicName, subscriptionName)
            {
                MaxDeliveryCount = maxDeliveryCount
            };

            try
            {
                await _administrationClient.CreateSubscriptionAsync(subscriptionOptions);
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
