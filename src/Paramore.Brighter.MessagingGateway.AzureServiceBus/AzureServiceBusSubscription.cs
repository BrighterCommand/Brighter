using System;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus
{
    public class AzureServiceBusSubscription : Subscription
    {
        public AzureServiceBusSubscription(
            Type dataType,
            SubscriptionName name = null,
            ChannelName channelName = null,
            RoutingKey routingKey = null,
            int bufferSize = 1,
            int noOfPerformers = 1,
            int timeoutInMilliseconds = 400,
            int requeueCount = -1,
            int requeueDelayInMilliseconds = 0,
            int unacceptableMessageLimit = 0,
            bool isAsync = false,
            IAmAChannelFactory channelFactory = null,
            OnMissingChannel makeChannels = OnMissingChannel.Create)
            : base(dataType, name, channelName, routingKey, bufferSize, noOfPerformers, timeoutInMilliseconds,
                requeueCount, requeueDelayInMilliseconds, unacceptableMessageLimit, isAsync, channelFactory,
                makeChannels)
        {
        }
    }

    public class AzureServiceBusSubscription<T> : AzureServiceBusSubscription where T : IRequest
    {
        public AzureServiceBusSubscription(
            SubscriptionName name = null,
            ChannelName channelName = null,
            RoutingKey routingKey = null,
            int bufferSize = 1,
            int noOfPerformers = 1,
            int timeoutInMilliseconds = 400,
            int requeueCount = -1,
            int requeueDelayInMilliseconds = 0,
            int unacceptableMessageLimit = 0,
            bool isAsync = false,
            IAmAChannelFactory channelFactory = null,
            OnMissingChannel makeChannels = OnMissingChannel.Create)
            : base(typeof(T), name, channelName, routingKey, bufferSize, noOfPerformers, timeoutInMilliseconds,
                requeueCount, requeueDelayInMilliseconds, unacceptableMessageLimit, isAsync, channelFactory,
                makeChannels)
        {
        }
    }
}
