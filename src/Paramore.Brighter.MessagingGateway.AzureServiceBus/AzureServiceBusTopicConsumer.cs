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
    public class AzureServiceBusTopicConsumer : AzureServiceBusConsumer
    {
        protected override ILogger Logger => s_logger;

        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<AzureServiceBusTopicConsumer>();
        private bool _subscriptionCreated;
        private readonly string _subscriptionName;
        private readonly IServiceBusReceiverProvider _serviceBusReceiverProvider;
        protected override string SubscriptionName => _subscriptionName;

        /// <summary>
        /// Initializes an Instance of <see cref="AzureServiceBusQueueConsumer"/> for Service Bus Topics
        /// </summary>
        /// <param name="subscription">An Azure Service Bus Subscription.</param>
        /// <param name="messageProducerSync">An instance of the Messaging Producer used for Requeue.</param>
        /// <param name="administrationClientWrapper">An Instance of Administration Client Wrapper.</param>
        /// <param name="serviceBusReceiverProvider">An Instance of <see cref="ServiceBusReceiverProvider"/>.</param>
        public AzureServiceBusTopicConsumer(AzureServiceBusSubscription subscription,
            IAmAMessageProducerSync messageProducerSync,
            IAdministrationClientWrapper administrationClientWrapper,
            IServiceBusReceiverProvider serviceBusReceiverProvider) : base(subscription,
            messageProducerSync, administrationClientWrapper)
        {
            _subscriptionName = subscription.ChannelName.Value;
            _serviceBusReceiverProvider = serviceBusReceiverProvider;
        }

        protected override void GetMessageReceiverProvider()
        {
            s_logger.LogInformation(
                "Getting message receiver provider for topic {Topic} and subscription {ChannelName}...",
                Topic, _subscriptionName);
            try
            {
                ServiceBusReceiver = _serviceBusReceiverProvider.Get(Topic, _subscriptionName,
                    SubscriptionConfiguration.RequireSession);
            }
            catch (Exception e)
            {
                s_logger.LogError(e,
                    "Failed to get message receiver provider for topic {Topic} and subscription {ChannelName}",
                    Topic, _subscriptionName);
            }
        }

        /// <summary>
        /// Purges the specified queue name.
        /// </summary>
        public override void Purge()
        {
            Logger.LogInformation("Purging messages from {Subscription} Subscription on Topic {Topic}",
                SubscriptionName, Topic);

            AdministrationClientWrapper.DeleteTopicAsync(Topic);
            EnsureChannel();
        }

        protected override void EnsureChannel()
        {
            if (_subscriptionCreated || Subscription.MakeChannels.Equals(OnMissingChannel.Assume))
                return;

            try
            {
                if (AdministrationClientWrapper.SubscriptionExists(Topic, _subscriptionName))
                {
                    _subscriptionCreated = true;
                    return;
                }

                if (Subscription.MakeChannels.Equals(OnMissingChannel.Validate))
                {
                    throw new ChannelFailureException(
                        $"Subscription {_subscriptionName} does not exist on topic {Topic} and missing channel mode set to Validate.");
                }

                AdministrationClientWrapper.CreateSubscription(Topic, _subscriptionName,
                    SubscriptionConfiguration);
                _subscriptionCreated = true;
            }
            catch (ServiceBusException ex)
            {
                if (ex.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
                {
                    s_logger.LogWarning(
                        "Message entity already exists with topic {Topic} and subscription {ChannelName}", Topic,
                        _subscriptionName);
                    _subscriptionCreated = true;
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
