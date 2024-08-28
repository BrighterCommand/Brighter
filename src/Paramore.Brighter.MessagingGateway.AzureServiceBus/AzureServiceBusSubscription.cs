using System;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus
{
    /// <summary>
    /// A <see cref="Subscription"/> with Specific option for Azure Service Bus.
    /// </summary>
    public class AzureServiceBusSubscription : Subscription
    {
        public AzureServiceBusSubscriptionConfiguration Configuration { get; }

        /// <summary>
        /// Initializes an Instance of <see cref="AzureServiceBusSubscription"/>
        /// </summary>
        /// <param name="dataType">The type for this Subscription.</param>
        /// <param name="name">The name. Defaults to the data type's full name.</param>
        /// <param name="channelName">The channel name. Defaults to the data type's full name.</param>
        /// <param name="routingKey">The routing key. Defaults to the data type's full name.</param>
        /// <param name="bufferSize">The number of messages to buffer on the channel</param>
        /// <param name="noOfPerformers">The no of performers.</param>
        /// <param name="timeoutInMilliseconds">The timeout in milliseconds.</param>
        /// <param name="requeueCount">The number of times you want to requeue a message before dropping it.</param>
        /// <param name="requeueDelayInMs">The number of milliseconds to delay the delivery of a requeue message for.</param>
        /// <param name="unacceptableMessageLimit">The number of unacceptable messages to handle, before stopping reading from the channel.</param>
        /// <param name="isAsync"></param>
        /// <param name="channelFactory">The channel factory to create channels for Consumer.</param>
        /// <param name="makeChannels">Should we make channels if they don't exist, defaults to creating</param>
        /// <param name="subscriptionConfiguration">The configuration options for the subscriptions.</param>
        /// <param name="emptyChannelDelay">How long to pause when a channel is empty in milliseconds</param>
        /// <param name="channelFailureDelay">How long to pause when there is a channel failure in milliseconds</param>
        public AzureServiceBusSubscription(
            Type dataType,
            SubscriptionName name = null,
            ChannelName channelName = null,
            RoutingKey routingKey = null,
            int bufferSize = 1,
            int noOfPerformers = 1,
            int timeoutInMilliseconds = 400,
            int requeueCount = -1,
            int requeueDelayInMs = 0,
            int unacceptableMessageLimit = 0,
            bool isAsync = false,
            IAmAChannelFactory channelFactory = null,
            OnMissingChannel makeChannels = OnMissingChannel.Create,
            AzureServiceBusSubscriptionConfiguration subscriptionConfiguration = null,
            int emptyChannelDelay = 500,
            int channelFailureDelay = 1000)
            : base(dataType, name, channelName, routingKey, bufferSize, noOfPerformers, timeoutInMilliseconds, requeueCount, requeueDelayInMs, unacceptableMessageLimit, isAsync, channelFactory,
                makeChannels, emptyChannelDelay, channelFailureDelay)
        {
            Configuration = subscriptionConfiguration ?? new AzureServiceBusSubscriptionConfiguration();
        }
    }

    /// <summary>
    /// Initializes an Instance of <see cref="AzureServiceBusSubscription"/>
    /// </summary>
    /// <typeparam name="T">The type of Subscription.</typeparam>
    public class AzureServiceBusSubscription<T> : AzureServiceBusSubscription where T : IRequest
    {
        /// <summary>
        /// Initializes an Instance of <see cref="AzureServiceBusSubscription"/>
        /// </summary>
        /// <param name="name">The name. Defaults to the data type's full name.</param>
        /// <param name="channelName">The channel name. Defaults to the data type's full name.</param>
        /// <param name="routingKey">The routing key. Defaults to the data type's full name.</param>
        /// <param name="bufferSize">The number of messages to buffer on the channel</param>
        /// <param name="noOfPerformers">The no of performers.</param>
        /// <param name="timeoutInMilliseconds">The timeout in milliseconds.</param>
        /// <param name="requeueCount">The number of times you want to requeue a message before dropping it.</param>
        /// <param name="requeueDelayInMs">The number of milliseconds to delay the delivery of a requeue message for.</param>
        /// <param name="unacceptableMessageLimit">The number of unacceptable messages to handle, before stopping reading from the channel.</param>
        /// <param name="isAsync"></param>
        /// <param name="channelFactory">The channel factory to create channels for Consumer.</param>
        /// <param name="makeChannels">Should we make channels if they don't exist, defaults to creating</param>
        /// <param name="subscriptionConfiguration">The configuration options for the subscriptions.</param>
        /// <param name="emptyChannelDelay">How long to pause when a channel is empty in milliseconds</param>
        /// <param name="channelFailureDelay">How long to pause when there is a channel failure in milliseconds</param>
        /// <param name="receiveMode">The mode in which to receive messages.</param>
        public AzureServiceBusSubscription(
            SubscriptionName name = null,
            ChannelName channelName = null,
            RoutingKey routingKey = null,
            int bufferSize = 1,
            int noOfPerformers = 1,
            int timeoutInMilliseconds = 400,
            int requeueCount = -1,
            int requeueDelayInMs = 0,
            int unacceptableMessageLimit = 0,
            bool isAsync = false,
            IAmAChannelFactory channelFactory = null,
            OnMissingChannel makeChannels = OnMissingChannel.Create,
            AzureServiceBusSubscriptionConfiguration subscriptionConfiguration = null,
            int emptyChannelDelay = 500,
            int channelFailureDelay = 1000)
            : base(typeof(T), name, channelName, routingKey, bufferSize, noOfPerformers,
                timeoutInMilliseconds, requeueCount, requeueDelayInMs, unacceptableMessageLimit,
                isAsync, channelFactory, makeChannels, subscriptionConfiguration, emptyChannelDelay, channelFailureDelay)
        {
        }
    }
}
