﻿using Microsoft.Azure.ServiceBus;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus
{
    public class AzureServiceBusConsumerFactory : IAmAMessageConsumerFactory
    {
        private readonly AzureServiceBusConfiguration _configuration;

        public AzureServiceBusConsumerFactory(AzureServiceBusConfiguration configuration)
        {
            _configuration = configuration;
        }
        
        public IAmAMessageConsumer Create(Subscription subscription)
        {
            var nameSpaceManagerWrapper = new ManagementClientWrapper(_configuration);

            return new AzureServiceBusConsumer(subscription.RoutingKey, subscription.ChannelName,
                new AzureServiceBusMessageProducer(nameSpaceManagerWrapper,
                    new TopicClientProvider(_configuration)), nameSpaceManagerWrapper,
                new MessageReceiverProvider(_configuration), receiveMode: _configuration.AckOnRead ? ReceiveMode.PeekLock : ReceiveMode.ReceiveAndDelete);
        }
    }
}
