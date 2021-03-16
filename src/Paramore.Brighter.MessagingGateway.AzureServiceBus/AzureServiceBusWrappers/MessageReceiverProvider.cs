using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers
{
    public class MessageReceiverProvider : IMessageReceiverProvider
    {
        private readonly string _connectionString;

        public MessageReceiverProvider(AzureServiceBusConfiguration configuration)
        {
            _connectionString = configuration.ConnectionString;
        }

        public IMessageReceiverWrapper Get(string topicName, string subscriptionName, ReceiveMode receiveMode)
        {
            var subscriptionPath = EntityNameHelper.FormatSubscriptionPath(topicName, subscriptionName);
            var messageReceiver = new MessageReceiver(_connectionString, subscriptionPath, receiveMode, RetryPolicy.Default);
            return new MessageReceiverWrapper(messageReceiver);
        }
    }
}
