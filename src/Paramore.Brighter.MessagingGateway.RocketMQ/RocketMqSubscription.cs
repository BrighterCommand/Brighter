using System;
using Org.Apache.Rocketmq;

namespace Paramore.Brighter.MessagingGateway.RocketMQ;

/// <summary>
/// Represents a RocketMQ subscription configuration for Brighter integration.
/// Implements RocketMQ's consumer group model and message visibility controls.
/// </summary>
public class RocketSubscription : Subscription
{
    /// <summary>
    /// Gets the consumer group name for RocketMQ message consumption.
    /// Identifies a group of consumers working as a cluster for parallel processing.
    /// </summary> 
    public string ConsumerGroup { get; }

    /// <summary>
    /// Gets the timeout for receiving messages from RocketMQ brokers.
    /// Controls the maximum time to wait for new messages during polling.
    /// </summary>
    public TimeSpan ReceiveMessageTimeout { get; }

    /// <summary>
    /// Gets the invisibility timeout for consumed messages.
    /// Determines how long messages remain invisible after being received.
    /// </summary>
    public TimeSpan InvisibilityTimeout { get; }

    /// <summary>
    /// Gets the message filter expression for RocketMQ topic filtering.
    /// Supports RocketMQ's tag-based or SQL-style message filtering.
    /// </summary>
    public FilterExpression Filter { get; }


    /// <inheritdoc />
    public override Type ChannelFactoryType => typeof(RocketMqChannelFactory);

    /// <summary>
    /// Initializes a new instance of the <see cref="Subscription"/> class.
    /// </summary>
    /// <param name="subscriptionName">The name. Defaults to the data type's full name.</param>
    /// <param name="channelName">The channel name. Defaults to the data type's full name.</param>
    /// <param name="routingKey">The routing key. Defaults to the data type's full name.</param>
    /// <param name="requestType">The <see cref="Type"/> of the data that this subscription handles.</param>
    /// <param name="getRequestType">The <see cref="Func{T,TResult}"/> that determines how we map a message to a type. Defaults to returning the <see cref="RequestType"/> if null</param>
    /// <param name="consumerGroup">What is the id of the consumer group that this consumer belongs to</param>
    /// <param name="bufferSize">The number of messages to buffer at any one time, also the number of messages to retrieve at once. Min of 1 Max of 10</param>
    /// <param name="noOfPerformers">The no of threads reading this channel.</param>
    /// <param name="timeOut">The timeout in milliseconds.</param>
    /// <param name="requeueCount">The number of times you want to requeue a message before dropping it.</param>
    /// <param name="requeueDelay">The delay the delivery of a requeue message. </param>
    /// <param name="unacceptableMessageLimit">The number of unacceptable messages to handle, before stopping reading from the channel.</param>
    /// <param name="messagePumpType">Is this channel read asynchronously</param>
    /// <param name="channelFactory">The channel factory to create channels for Consumer.</param>
    /// <param name="makeChannels">Should we make channels if they don't exist, defaults to creating</param>
    /// <param name="filter">The subscription filter</param>
    /// <param name="emptyChannelDelay">How long to pause when a channel is empty in milliseconds</param>
    /// <param name="channelFailureDelay">How long to pause when there is a channel failure in milliseconds</param>
    /// <param name="receiveMessageTimeout">How long it will wait to receive a message.</param>
    /// <param name="invisibilityTimeout">How long the RocketMQ should wait before retry.</param>
    public RocketSubscription(SubscriptionName subscriptionName,
        ChannelName channelName,
        RoutingKey routingKey,
        Type? requestType = null,
        Func<Message, Type>? getRequestType = null,
        string? consumerGroup = null,
        int bufferSize = 1,
        int noOfPerformers = 1,
        TimeSpan? timeOut = null,
        int requeueCount = -1,
        TimeSpan? requeueDelay = null,
        int unacceptableMessageLimit = 0,
        MessagePumpType messagePumpType = MessagePumpType.Unknown,
        IAmAChannelFactory? channelFactory = null,
        OnMissingChannel makeChannels = OnMissingChannel.Create,
        FilterExpression? filter = null,
        TimeSpan? emptyChannelDelay = null,
        TimeSpan? channelFailureDelay = null,
        TimeSpan? receiveMessageTimeout = null,
        TimeSpan? invisibilityTimeout = null) : base(subscriptionName, channelName, routingKey, requestType, getRequestType,
        bufferSize, noOfPerformers, timeOut, requeueCount, requeueDelay, unacceptableMessageLimit, messagePumpType, channelFactory,
        makeChannels, emptyChannelDelay, channelFailureDelay)
    {
        ConsumerGroup = consumerGroup ?? string.Empty;
        ReceiveMessageTimeout = receiveMessageTimeout ?? TimeSpan.FromMinutes(1);
        InvisibilityTimeout = invisibilityTimeout ?? TimeSpan.FromSeconds(30);
        Filter = filter ?? new FilterExpression("*");
    }
}

/// <summary>
/// Represents a RocketMQ subscription configuration for Brighter integration.
/// Implements RocketMQ's consumer group model and message visibility controls.
/// </summary>
/// <typeparam name="T"></typeparam>
public class RocketMqSubscription<T> : RocketSubscription
    where T : class, IRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Subscription"/> class.
    /// </summary>
    /// <param name="subscriptionName">The name. Defaults to the data type's full name.</param>
    /// <param name="channelName">The channel name. Defaults to the data type's full name.</param>
    /// <param name="routingKey">The routing key. Defaults to the data type's full name.</param>
    /// <param name="getRequestType">The <see cref="Func{T,TResult}"/> that determines how we map a message to a type. Defaults to returning the <see cref="RequestType"/> if null</param>
    /// <param name="consumerGroup">What is the id of the consumer group that this consumer belongs to</param>
    /// <param name="bufferSize">The number of messages to buffer at any one time, also the number of messages to retrieve at once. Min of 1 Max of 10</param>
    /// <param name="noOfPerformers">The no of threads reading this channel.</param>
    /// <param name="timeOut">The timeout in milliseconds.</param>
    /// <param name="requeueCount">The number of times you want to requeue a message before dropping it.</param>
    /// <param name="requeueDelay">The delay the delivery of a requeue message. </param>
    /// <param name="unacceptableMessageLimit">The number of unacceptable messages to handle, before stopping reading from the channel.</param>
    /// <param name="messagePumpType">Is this channel read asynchronously</param>
    /// <param name="channelFactory">The channel factory to create channels for Consumer.</param>
    /// <param name="makeChannels">Should we make channels if they don't exist, defaults to creating</param>
    /// <param name="filter">The subscription filter</param>
    /// <param name="emptyChannelDelay">How long to pause when a channel is empty in milliseconds</param>
    /// <param name="channelFailureDelay">How long to pause when there is a channel failure in milliseconds</param>
    /// <param name="receiveMessageTimeout">How long it will wait to receive a message.</param>
    /// <param name="invisibilityTimeout">How long the RocketMQ should wait before retry.</param>
    public RocketMqSubscription(SubscriptionName subscriptionName, ChannelName channelName, RoutingKey routingKey,
        Func<Message, Type>? getRequestType = null, string? consumerGroup = null, int bufferSize = 1, int noOfPerformers = 1,
        TimeSpan? timeOut = null, int requeueCount = -1, TimeSpan? requeueDelay = null,
        int unacceptableMessageLimit = 0, MessagePumpType messagePumpType = MessagePumpType.Unknown,
        IAmAChannelFactory? channelFactory = null, OnMissingChannel makeChannels = OnMissingChannel.Create,
        FilterExpression? filter = null, TimeSpan? emptyChannelDelay = null,
        TimeSpan? channelFailureDelay = null, TimeSpan? receiveMessageTimeout = null,
        TimeSpan? invisibilityTimeout = null) : base(subscriptionName, channelName, routingKey, typeof(T),  getRequestType,
        consumerGroup, bufferSize, noOfPerformers, timeOut, requeueCount, requeueDelay, unacceptableMessageLimit,
        messagePumpType, channelFactory, makeChannels, filter, emptyChannelDelay, channelFailureDelay, receiveMessageTimeout,
        invisibilityTimeout)
    {
    }
}
