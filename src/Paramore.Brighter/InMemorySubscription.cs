using System;

namespace Paramore.Brighter;

/// <summary>
/// A derived type of <see cref="Subscription"/> that adds an implementation of <see cref="IUseBrighterDeadLetterSupport"/>
/// and <see cref="IUseBrighterInvalidMessageSupport"/> which indicate that Brighter supports dead letter and
/// invalid message channels, for the InMemoryConsumer
/// </summary>
/// <param name="subscriptionName">The name. Defaults to the data type's full name.</param>
/// <param name="channelName">The channel name. Defaults to the data type's full name.</param>
/// <param name="routingKey">The routing key. Defaults to the data type's full name.</param>
/// <param name="noOfPerformers">The no of performers.</param>
/// <param name="getRequestType">The <see cref="Func{T,TResult}"/> that determines how we map a message to a type. Defaults to returning the <see cref="T"/> if null</param>
/// <param name="bufferSize">The number of messages to buffer on the channel</param>
/// <param name="timeOut">The timeout before we consider the subscription empty and pause</param>
/// <param name="requeueCount">The number of times you want to requeue a message before dropping it.</param>
/// <param name="requeueDelay">The delay the delivery of a requeue message; defaults to 0ms</param>
/// <param name="unacceptableMessageLimit">The number of unacceptable messages to handle, before stopping reading from the channel.</param>
/// <param name="messagePumpType"></param>
/// <param name="channelFactory">The channel factory to create channels for Consumer.</param>
/// <param name="makeChannels">Should we make channels if they don't exist, defaults to creating</param>
/// <param name="emptyChannelDelay">How long to pause when a channel is empty in milliseconds</param>
/// <param name="channelFailureDelay">How long to pause when there is a channel failure in milliseconds</param>

public class InMemorySubscription(
    SubscriptionName subscriptionName,
    ChannelName channelName,
    RoutingKey routingKey,
    Type? requestType = null,
    Func<Message, Type>? getRequestType = null,
    int bufferSize = 1,
    int noOfPerformers = 1,
    TimeSpan? timeOut = null,
    int requeueCount = -1,
    TimeSpan? requeueDelay = null,
    int unacceptableMessageLimit = 0,
    TimeSpan? unacceptableMessageLimitWindow = null,
    MessagePumpType messagePumpType = MessagePumpType.Unknown,
    IAmAChannelFactory? channelFactory = null,
    OnMissingChannel makeChannels = OnMissingChannel.Create,
    TimeSpan? emptyChannelDelay = null,
    TimeSpan? channelFailureDelay = null)
    : Subscription(subscriptionName, channelName, routingKey, requestType, getRequestType, bufferSize, noOfPerformers,
        timeOut, requeueCount, requeueDelay, unacceptableMessageLimit, messagePumpType, channelFactory, makeChannels,
        emptyChannelDelay, channelFailureDelay, unacceptableMessageLimitWindow), IUseBrighterDeadLetterSupport, IUseBrighterInvalidMessageSupport
{
    /// <summary>
    /// The <see cref="RoutingKey"/> for the Dead Letter Channel
    /// </summary>
    public RoutingKey? DeadLetterRoutingKey { get; set; }
    
    /// <summary>
    /// The <see cref="RoutingKey"/> for the Invalid Message Channel
    /// </summary>
    public RoutingKey? InvalidMessageRoutingKey { get; set; }
}

/// <summary>
/// Initializes a generic version of <see cref="InMemorySubscription"/>
/// </summary>
/// <param name="subscriptionName">The name. Defaults to the data type's full name.</param>
/// <param name="channelName">The channel name. Defaults to the data type's full name.</param>
/// <param name="routingKey">The routing key. Defaults to the data type's full name.</param>
/// <param name="noOfPerformers">The no of performers.</param>
/// <param name="getRequestType">The <see cref="Func{T,TResult}"/> that determines how we map a message to a type. Defaults to returning the <see cref="T"/> if null</param>
/// <param name="bufferSize">The number of messages to buffer on the channel</param>
/// <param name="timeOut">The timeout before we consider the subscription empty and pause</param>
/// <param name="requeueCount">The number of times you want to requeue a message before dropping it.</param>
/// <param name="requeueDelay">The delay the delivery of a requeue message; defaults to 0ms</param>
/// <param name="unacceptableMessageLimit">The number of unacceptable messages to handle, before stopping reading from the channel.</param>
/// <param name="messagePumpType"></param>
/// <param name="channelFactory">The channel factory to create channels for Consumer.</param>
/// <param name="makeChannels">Should we make channels if they don't exist, defaults to creating</param>
/// <param name="emptyChannelDelay">How long to pause when a channel is empty in milliseconds</param>
/// <param name="channelFailureDelay">How long to pause when there is a channel failure in milliseconds</param>
/// <typeparam name="T"></typeparam>
public class InMemorySubscription<T>(
    SubscriptionName subscriptionName,
    ChannelName channelName,
    RoutingKey routingKey,
    Func<Message, Type>? getRequestType = null,
    int bufferSize = 1,
    int noOfPerformers = 1,
    TimeSpan? timeOut = null,
    int requeueCount = -1,
    TimeSpan? requeueDelay = null,
    int unacceptableMessageLimit = 0,
    TimeSpan? unacceptableMessageLimitWindow = null,
    MessagePumpType messagePumpType = MessagePumpType.Unknown,
    IAmAChannelFactory? channelFactory = null,
    OnMissingChannel makeChannels = OnMissingChannel.Create,
    TimeSpan? emptyChannelDelay = null, TimeSpan? channelFailureDelay = null)
    : InMemorySubscription(subscriptionName, channelName, routingKey, typeof(T), getRequestType, bufferSize,
        noOfPerformers, timeOut, requeueCount, requeueDelay, unacceptableMessageLimit, unacceptableMessageLimitWindow, messagePumpType,
        channelFactory, makeChannels, emptyChannelDelay, channelFailureDelay)
    where T : class, IRequest
{}
