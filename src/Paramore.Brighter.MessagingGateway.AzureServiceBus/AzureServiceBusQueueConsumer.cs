using System;
using System.Collections.Generic;
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
        
        /// <summary>
        /// Initializes an Instance of <see cref="AzureServiceBusQueueConsumer"/> for Service Bus Queus
        /// </summary>
        /// <param name="subscription">An Azure Service Bus Subscription.</param>
        /// <param name="messageProducerSync">An instance of the Messaging Producer used for Requeue.</param>
        /// <param name="administrationClientWrapper">An Instance of Administration Client Wrapper.</param>
        /// <param name="serviceBusReceiverProvider">An Instance of <see cref="ServiceBusReceiverProvider"/>.</param>
        /// <param name="receiveMode">The mode in which to Receive.</param>
        public AzureServiceBusQueueConsumer(AzureServiceBusSubscription subscription,
            IAmAMessageProducerSync messageProducerSync,
            IAdministrationClientWrapper administrationClientWrapper,
            IServiceBusReceiverProvider serviceBusReceiverProvider,
            ServiceBusReceiveMode receiveMode = ServiceBusReceiveMode.ReceiveAndDelete) : base(subscription,
            messageProducerSync, administrationClientWrapper, receiveMode)
        {
            _serviceBusReceiverProvider = serviceBusReceiverProvider;

            if (!_subscriptionConfiguration.RequireSession)
                GetMessageReceiverProvider();
        }

        protected override void GetMessageReceiverProvider()
        {
            s_logger.LogInformation(
                "Getting message receiver provider for topic {Topic} with receive Mode {ReceiveMode}...",
                TopicName, _receiveMode);
            try
            {
                ServiceBusReceiver = _serviceBusReceiverProvider.Get(TopicName, _receiveMode,
                        _subscriptionConfiguration.RequireSession);
            }
            catch (Exception e)
            {
                s_logger.LogError(e, "Failed to get message receiver provider for topic {Topic}.", TopicName);
            }
        }
        
        /// <summary>
        /// Purges the specified queue name.
        /// </summary>
        public override void Purge()
        {
            Logger.LogInformation("Purging messages from {Subscription} Subscription on Topic {Topic}", 
                SubscriptionName, TopicName);

            AdministrationClientWrapper.DeleteQueueAsync(TopicName);
            EnsureSubscription();
        }

        protected override void EnsureSubscription()
        {
            //No Subscription To Create
            return;
        }
    }
}
