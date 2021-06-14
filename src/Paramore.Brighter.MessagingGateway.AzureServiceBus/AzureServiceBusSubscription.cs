using System;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus
{
    public class AzureServiceBusSubscription : Subscription
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AzureServiceBusSubscription"/> class 
        /// </summary>
        /// <param name="dataType">Type of the data.</param>
        /// <param name="name">The name. Defaults to the data type's full name.</param>
        /// <param name="channelName">The channel name. Defaults to the data type's full name.</param>
        /// <param name="routingKey">The routing key. Defaults to the data type's full name.</param>
        /// <param name="noOfPerformers">The no of performers.</param>
        /// <param name="bufferSize">The number of messages to buffer on the channel</param>
        /// <param name="timeoutInMs">The timeout in milliseconds.</param>
        /// <param name="pollDelayInMs">Interval between polling attempts</param>
        /// <param name="noWorkPauseInMs">When a queue is empty, delay this long before re-reading from the queue</param>
        /// <param name="requeueCount">The number of times you want to requeue a message before dropping it.</param>
        /// <param name="requeueDelayInMs">The number of milliseconds to delay the delivery of a requeue message for.</param>
        /// <param name="unacceptableMessageLimit">The number of unacceptable messages to handle, before stopping reading from the channel.</param>
        /// <param name="runAsync"></param>
        /// <param name="channelFactory">The channel factory to create channels for Consumer.</param>
        /// <param name="makeChannels">Should we make channels if they don't exist, defaults to creating</param>
        public AzureServiceBusSubscription(
            Type dataType,
            SubscriptionName name = null,
            ChannelName channelName = null,
            RoutingKey routingKey = null,
            int bufferSize = 1,
            int noOfPerformers = 1,
            int timeoutInMs = 300,
            int pollDelayInMs = -1,
            int noWorkPauseInMs = 500,
            int requeueCount = -1,
            int requeueDelayInMs = 0,
            int unacceptableMessageLimit = 0,
            bool isAsync = false,
            IAmAChannelFactory channelFactory = null,
            OnMissingChannel makeChannels = OnMissingChannel.Create)
            : base(dataType, name, channelName, routingKey, bufferSize, noOfPerformers, timeoutInMs, pollDelayInMs,
                noWorkPauseInMs, requeueCount, requeueDelayInMs, unacceptableMessageLimit, isAsync, channelFactory,
                makeChannels)
        {
        }
    }


    public class AzureServiceBusSubscription<T> : AzureServiceBusSubscription where T : IRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AzureServiceBusSubscription"/> class with data type T.
        /// </summary>
        /// <param name="name">The name. Defaults to the data type's full name.</param>
        /// <param name="channelName">The channel name. Defaults to the data type's full name.</param>
        /// <param name="routingKey">The routing key. Defaults to the data type's full name.</param>
        /// <param name="noOfPerformers">The no of performers.</param>
        /// <param name="bufferSize">The number of messages to buffer on the channel</param>
        /// <param name="timeoutInMs">The timeout in milliseconds.</param>
        /// <param name="pollDelayInMs">Interval between polling attempts</param>
        /// <param name="noWorkPauseInMs">When a queue is empty, delay this long before re-reading from the queue</param>
        /// <param name="requeueCount">The number of times you want to requeue a message before dropping it.</param>
        /// <param name="requeueDelayInMs">The number of milliseconds to delay the delivery of a requeue message for.</param>
        /// <param name="unacceptableMessageLimit">The number of unacceptable messages to handle, before stopping reading from the channel.</param>
        /// <param name="runAsync"></param>
        /// <param name="channelFactory">The channel factory to create channels for Consumer.</param>
        /// <param name="makeChannels">Should we make channels if they don't exist, defaults to creating</param>
        public AzureServiceBusSubscription(
            SubscriptionName name = null,
            ChannelName channelName = null,
            RoutingKey routingKey = null,
            int bufferSize = 1,
            int noOfPerformers = 1,
            int timeoutInMs = 300,
            int pollDelayInMs = -1,
            int noWorkPauseInMs = 500,
            int requeueCount = -1,
            int requeueDelayInMs = 0,
            int unacceptableMessageLimit = 0,
            bool isAsync = false,
            IAmAChannelFactory channelFactory = null,
            OnMissingChannel makeChannels = OnMissingChannel.Create)
            : base(typeof(T), name, channelName, routingKey, bufferSize, noOfPerformers, timeoutInMs,
                pollDelayInMs, noWorkPauseInMs, requeueCount, requeueDelayInMs, unacceptableMessageLimit,
                isAsync, channelFactory, makeChannels)
        {
        }
    }
}
