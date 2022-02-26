using System;

namespace Paramore.Brighter.MessagingGateway.RedisStreams
{
    public class RedisStreamsSubscription : Subscription
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Subscription"/> class.
        /// </summary>
        /// <param name="dataType">Type of the data.</param>
        /// <param name="name">The name. Defaults to the data type's full name.</param>
        /// <param name="channelName">The channel name. Defaults to the data type's full name.</param>
        /// <param name="routingKey">The routing key. Defaults to the data type's full name.</param>
        /// <param name="bufferSize">The number of messages to buffer at any one time, also the number of messages to retrieve at once. Min of 1 Max of 10</param>
        /// <param name="noOfPerformers">The no of threads reading this channel.</param>
        /// <param name="timeoutInMilliseconds">The timeout in milliseconds.</param>
        /// <param name="requeueCount">The number of times you want to requeue a message before dropping it.</param>
        /// <param name="requeueDelayInMilliseconds">The number of milliseconds to delay the delivery of a requeue message for.</param>
        /// <param name="unacceptableMessageLimit">The number of unacceptable messages to handle, before stopping reading from the channel.</param>
        /// <param name="runAsync">Is this channel read asynchronously</param>
        /// <param name="channelFactory">The channel factory to create channels for Consumer.</param>
        /// <param name="makeChannels">Should we make channels if they don't exist, defaults to creating</param>
        public RedisStreamsSubscription(
            Type dataType, 
            SubscriptionName name = null, 
            ChannelName channelName = null, 
            RoutingKey routingKey = null, 
            int bufferSize = 1, 
            int noOfPerformers = 1, 
            int timeoutInMilliseconds = 300, 
            int requeueCount = -1, 
            int requeueDelayInMilliseconds = 0,
            int unacceptableMessageLimit = 0, 
            bool runAsync = false, 
            IAmAChannelFactory channelFactory = null, 
            OnMissingChannel makeChannels = OnMissingChannel.Create) 
            : base(dataType, name, channelName, routingKey, bufferSize, noOfPerformers, timeoutInMilliseconds, requeueCount, requeueDelayInMilliseconds, unacceptableMessageLimit, runAsync, channelFactory, makeChannels)
        {
        }
    }
}
