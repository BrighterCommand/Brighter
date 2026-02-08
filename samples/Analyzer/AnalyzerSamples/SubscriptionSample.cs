

using Paramore.Brighter;

namespace AnalyzerSamples
{
    internal class SubscriptionSample
    {
    }
    public static class SubscriptionCreator
    {
        public static Subscription GetSubscription_MessagePumpSpecified()
        {
            return new SubscriptionTest("name", "name", "key", messagePumpType: MessagePumpType.Reactor);
        }
        public static Subscription GetSubscription_NoMessagePumpSpecified()
        {
            return new SubscriptionTest("name", "name", "key");
        }
        public static Subscription GetSubscriptionNested_NoMessagePumpSpecified()
        {
            return new SubscriptionTestNested("name", "name", "key");
        }
        public class SubscriptionTest : Subscription
        {
            public SubscriptionTest(SubscriptionName subscriptionName, ChannelName channelName, RoutingKey routingKey, Type? requestType = null, Func<Message, Type>? getRequestType = null, int bufferSize = 1, int noOfPerformers = 1, TimeSpan? timeOut = null, int requeueCount = -1, TimeSpan? requeueDelay = null, int unacceptableMessageLimit = 0, MessagePumpType messagePumpType = MessagePumpType.Unknown, IAmAChannelFactory? channelFactory = null, OnMissingChannel makeChannels = OnMissingChannel.Create, TimeSpan? emptyChannelDelay = null, TimeSpan? channelFailureDelay = null) : base(subscriptionName, channelName, routingKey, requestType, getRequestType, bufferSize, noOfPerformers, timeOut, requeueCount, requeueDelay, unacceptableMessageLimit, messagePumpType, channelFactory, makeChannels, emptyChannelDelay, channelFailureDelay)
            {
            }
        }
        public class SubscriptionTestNested : SubscriptionTest
        {
            public SubscriptionTestNested(SubscriptionName subscriptionName, ChannelName channelName, RoutingKey routingKey, Type? requestType = null, Func<Message, Type>? getRequestType = null, int bufferSize = 1, int noOfPerformers = 1, TimeSpan? timeOut = null, int requeueCount = -1, TimeSpan? requeueDelay = null, int unacceptableMessageLimit = 0, MessagePumpType messagePumpType = MessagePumpType.Unknown, IAmAChannelFactory? channelFactory = null, OnMissingChannel makeChannels = OnMissingChannel.Create, TimeSpan? emptyChannelDelay = null, TimeSpan? channelFailureDelay = null) : base(subscriptionName, channelName, routingKey, requestType, getRequestType, bufferSize, noOfPerformers, timeOut, requeueCount, requeueDelay, unacceptableMessageLimit, messagePumpType, channelFactory, makeChannels, emptyChannelDelay, channelFailureDelay)
            {
            }
        }
    }
}
