using System;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus
{
    /// <summary>
    /// Implementation of <see cref="IAmAMessageConsumer"/> using Azure Service Bus for Transport.
    /// </summary>
    public class AzureServiceBusQueueConsumer : AzureServiceBusConsumer
    {
        protected override string SubscriptionName => "Queue";
        protected override ILogger Logger => s_logger;
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<AzureServiceBusQueueConsumer>();
        private readonly IServiceBusReceiverProvider _serviceBusReceiverProvider;

        private bool _queueCreated = false;
        
        /// <summary>
        /// Initializes an Instance of <see cref="AzureServiceBusQueueConsumer"/> for Service Bus Queus
        /// </summary>
        /// <param name="subscription">An Azure Service Bus Subscription.</param>
        /// <param name="messageProducerSync">An instance of the Messaging Producer used for Requeue.</param>
        /// <param name="administrationClientWrapper">An Instance of Administration Client Wrapper.</param>
        /// <param name="serviceBusReceiverProvider">An Instance of <see cref="ServiceBusReceiverProvider"/>.</param>
        public AzureServiceBusQueueConsumer(AzureServiceBusSubscription subscription,
            IAmAMessageProducerSync messageProducerSync,
            IAdministrationClientWrapper administrationClientWrapper,
            IServiceBusReceiverProvider serviceBusReceiverProvider) : base(subscription,
            messageProducerSync, administrationClientWrapper)
        {
            _serviceBusReceiverProvider = serviceBusReceiverProvider;
        }

        protected override void GetMessageReceiverProvider()
        {
            s_logger.LogInformation(
                "Getting message receiver provider for queue {Queue}...",
                Topic);
            try
            {
                ServiceBusReceiver = _serviceBusReceiverProvider.Get(Topic,
                        SubscriptionConfiguration.RequireSession);
            }
            catch (Exception e)
            {
                s_logger.LogError(e, "Failed to get message receiver provider for queue {Queue}", Topic);
            }
        }
        
        /// <summary>
        /// Purges the specified queue name.
        /// </summary>
        public override void Purge()
        {
            Logger.LogInformation("Purging messages from Queue {Queue}", 
                Topic);

            AdministrationClientWrapper.DeleteQueueAsync(Topic);
            EnsureChannel();
        }

        protected override void EnsureChannel()
        {
            if (_queueCreated || Subscription.MakeChannels.Equals(OnMissingChannel.Assume))
                return;

            try
            {
                if (AdministrationClientWrapper.QueueExists(Topic))
                {
                    _queueCreated = true;
                    return;
                }

                if (Subscription.MakeChannels.Equals(OnMissingChannel.Validate))
                {
                    throw new ChannelFailureException(
                        $"Queue {Topic} does not exist and missing channel mode set to Validate.");
                }

                AdministrationClientWrapper.CreateQueue(Topic, SubscriptionConfiguration.QueueIdleBeforeDelete);
                _queueCreated = true;
            }
            catch (ServiceBusException ex)
            {
                if (ex.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
                {
                    s_logger.LogWarning(
                        "Message entity already exists with queue {Queue}", Topic);
                    _queueCreated = true;
                }
                else
                {
                    throw new ChannelFailureException("Failing to check or create subscription", ex);
                }
            }
            catch (Exception e)
            {
                s_logger.LogError(e, "Failing to check or create subscription");

                //The connection to Azure Service bus may have failed so we re-establish the connection.
                AdministrationClientWrapper.Reset();

                throw new ChannelFailureException("Failing to check or create subscription", e);
            }
        }
    }
}
