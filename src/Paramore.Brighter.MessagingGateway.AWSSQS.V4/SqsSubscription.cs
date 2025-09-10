#region Licence

/* The MIT License (MIT)
Copyright © 2020 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;

namespace Paramore.Brighter.MessagingGateway.AWSSQS.V4;

/// <summary>
/// A subscription for an SQS Consumer.
/// We will create infrastructure on the basis of Make Channels
/// Create = topic using routing key name, queue using channel name
/// Validate = look for topic using routing key name, queue using channel name
/// Assume = Assume Routing Key is Topic ARN, queue exists via channel name
/// </summary>
public class SqsSubscription : Subscription
{
    /// <summary>
    /// The routing key type.
    /// </summary>
    public ChannelType ChannelType { get; }

    /// <summary>
    /// Indicates how we should treat the <see cref="RoutingKey"/>
    /// TopicFindBy.Arn -> the routing key is an Arn; assumes that you know it exists and provide it
    /// TopicFindBy.Convention -> The routing key is a name, but use convention to make an Arn for this account
    /// TopicFindBy.Name -> Treat the routing key as a name & use ListTopics to find it (rate limited 30/s)
    /// </summary>
    public TopicFindBy FindTopicBy { get; set; }

    /// <summary>
    /// Indicates how we should treat the  <see cref="ChannelName"/>
    /// QueueFindBy.Url -> The Channel Name contains the Url for a queue; assumes that you know it exists and provide it
    /// QueueFindBy.Name -> The Channel Name contains the name of the queue; we will look up the queue Url
    /// </summary>
    public QueueFindBy FindQueueBy { get; set; }

    /// <summary>
    /// The underlying <see cref="QueueAttributes"/> definition. This is used to create the queue if it does not exist.
    /// </summary>
    public SqsAttributes QueueAttributes { get; }

    /// <summary>
    /// The attributes of the topic; used if the <see cref="ChannelType"/> is <see cref="ChannelType.PubSub"/>
    /// </summary>
    public SnsAttributes? TopicAttributes { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Subscription"/> class.
    /// </summary>
    /// <param name="subscriptionName">The name. Defaults to the data type's full name.</param>
    /// <param name="channelName">The channel name. Defaults to the data type's full name.</param>
    /// <param name="channelType">Specifies the routing key type</param>
    /// <param name="routingKey">The routing key. Defaults to the data type's full name.</param>
    /// <param name="requestType">Type of the data.</param>
    /// <param name="getRequestType">The <see cref="Func{Message, Type}"/> that determines how we map a message to a type. Defaults to returning the <paramref name="requestType"/> if null</param>
    /// <param name="bufferSize">The number of messages to buffer at any one time, also the number of messages to retrieve at once. Min of 1 Max of 10</param>
    /// <param name="noOfPerformers">The no of threads reading this channel.</param>
    /// <param name="timeOut">The timeout that infers nothing could be read from the queue.</param>
    /// <param name="requeueCount">The number of times you want to requeue a message before dropping it.</param>
    /// <param name="requeueDelay">The number of milliseconds to delay the delivery of a requeue message for.</param>
    /// <param name="unacceptableMessageLimit">The number of unacceptable messages to handle, before stopping reading from the channel.</param>
    /// <param name="messagePumpType">Is this channel read asynchronously</param>
    /// <param name="channelFactory">The channel factory to create channels for Consumer.</param>
    /// <param name="emptyChannelDelay">How long to pause when a channel is empty in milliseconds</param>
    /// <param name="channelFailureDelay">How long to pause when there is a channel failure in milliseconds</param>
    /// <param name="findTopicBy">Is the Topic an Arn, should be treated as an Arn by convention, or a name</param>
    /// <param name="findQueueBy">Is the ChannelName is a queue url, or a name</param>
    /// <param name="queueAttributes">What are the <see cref="SqsAttributes"/> of the Sqs queue we use to receive messages</param>
    /// <param name="topicAttributes">What are the <see cref="SnsAttributes"/>  of the topic to which are queue subscribes, if we are <see cref="ChannelType.PubSub"/> </param>
    /// <param name="makeChannels">Should we make channels if they don't exist, defaults to creating</param>
    public SqsSubscription(
        SubscriptionName subscriptionName,
        ChannelName channelName,
        ChannelType channelType,
        RoutingKey routingKey,
        Type? requestType = null,
        Func<Message, Type>? getRequestType = null,
        int bufferSize = 1,
        int noOfPerformers = 1,
        TimeSpan? timeOut = null,
        int requeueCount = -1,
        TimeSpan? requeueDelay = null,
        int unacceptableMessageLimit = 0,
        MessagePumpType messagePumpType = MessagePumpType.Unknown,
        IAmAChannelFactory? channelFactory = null,
        TimeSpan? emptyChannelDelay = null,
        TimeSpan? channelFailureDelay = null,
        TopicFindBy findTopicBy = TopicFindBy.Convention,
        QueueFindBy findQueueBy = QueueFindBy.Name,
        SqsAttributes? queueAttributes = null,
        SnsAttributes? topicAttributes = null,
        OnMissingChannel makeChannels = OnMissingChannel.Create)
        : base(subscriptionName, channelName, routingKey, requestType, getRequestType, bufferSize, noOfPerformers,
            timeOut, requeueCount, requeueDelay, unacceptableMessageLimit, messagePumpType, channelFactory,
            makeChannels, emptyChannelDelay, channelFailureDelay)
    {
        if (channelType == ChannelType.PubSub && routingKey is null)
            throw new ArgumentNullException(nameof(routingKey), "Routing Key is required for PubSub channels");

        if (channelType == ChannelType.PointToPoint && channelName is null)
            throw new ArgumentNullException(nameof(channelName), "Channel Name is required for PointToPoint channels");

        QueueAttributes = queueAttributes ?? SqsAttributes.Empty;
        TopicAttributes = topicAttributes ?? SnsAttributes.Empty;

        if (channelType == ChannelType.PubSub && QueueAttributes.Type != TopicAttributes.Type)
            throw new InvalidOperationException(" For PubSub channels, you must set both queue and topic attribute to the same Type (default Standard)");

        ChannelType = channelType;
        FindTopicBy = findTopicBy;
        FindQueueBy = findQueueBy;
    }
}

/// <summary>
/// A subscription for an SQS Consumer.
/// We will create infrastructure on the basis of Make Channels
/// Create = topic using routing key name, queue using channel name
/// Validate = look for topic using routing key name, queue using channel name
/// Assume = Assume Routing Key is Topic ARN, queue exists via channel name
/// </summary>
public class SqsSubscription<T> : SqsSubscription where T : IRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Subscription"/> class.
    /// </summary>
    /// <param name="getRequestType">The <see cref="Func{Message, Type}"/> that determines how we map a message to a type. Defaults to returning the <see cref="T"/> if null</param>
    /// <param name="subscriptionName">The name. Defaults to the data type's full name.</param>
    /// <param name="channelName">The channel name. Defaults to the data type's full name.</param>
    /// <param name="channelType">Specifies the routing key type</param>
    /// <param name="routingKey">The routing key. Defaults to the data type's full name.</param>
    /// <param name="bufferSize">The number of messages to buffer at any one time, also the number of messages to retrieve at once. Min of 1 Max of 10</param>
    /// <param name="noOfPerformers">The no of threads reading this channel.</param>
    /// <param name="timeOut">The timeout that infers nothing could be read from the queue. Defaults to 300 milliseconds.</param>
    /// <param name="requeueCount">The number of times you want to requeue a message before dropping it.</param>
    /// <param name="requeueDelay">The number of milliseconds to delay the delivery of a requeue message for.</param>
    /// <param name="unacceptableMessageLimit">The number of unacceptable messages to handle, before stopping reading from the channel.</param>
    /// <param name="messagePumpType">Is this channel read asynchronously</param>
    /// <param name="channelFactory">The channel factory to create channels for Consumer.</param>
    /// <param name="emptyChannelDelay">How long to pause when a channel is empty in milliseconds</param>
    /// <param name="channelFailureDelay">How long to pause when there is a channel failure in milliseconds</param>
    /// <param name="findTopicBy">Is the Topic an Arn, should be treated as an Arn by convention, or a name. Default is convention</param>
    /// <param name="findQueueBy">Is the ChannelName is a queue url, or a name. Default is by name</param>
    /// <param name="queueAttributes">What are the details of the <see cref="SqsAttributes"/> that exchanges requests</param>
    /// <param name="topicAttributes">What are the <see cref="SnsAttributes"/>  of the topic to which are queue subscribes, if we are <see cref="ChannelType.PubSub"/> </param>
    /// <param name="makeChannels">Should we make channels if they don't exist, defaults to creating</param>
    public SqsSubscription(
        Func<Message, Type>? getRequestType = null,
        SubscriptionName? subscriptionName = null,
        ChannelName? channelName = null,
        ChannelType channelType = ChannelType.PubSub,
        RoutingKey? routingKey = null,
        int bufferSize = 1,
        int noOfPerformers = 1,
        TimeSpan? timeOut = null,
        int requeueCount = -1,
        TimeSpan? requeueDelay = null,
        int unacceptableMessageLimit = 0,
        MessagePumpType messagePumpType = MessagePumpType.Proactor,
        IAmAChannelFactory? channelFactory = null,
        TimeSpan? emptyChannelDelay = null,
        TimeSpan? channelFailureDelay = null,
        TopicFindBy findTopicBy = TopicFindBy.Convention,
        QueueFindBy findQueueBy = QueueFindBy.Name,
        SqsAttributes? queueAttributes = null,
        SnsAttributes? topicAttributes = null,
        OnMissingChannel makeChannels = OnMissingChannel.Create)
        : base(
            subscriptionName ?? new SubscriptionName(typeof(T).FullName!),
            channelName ?? new ChannelName(typeof(T).FullName!),
            channelType,
            routingKey ?? new RoutingKey(typeof(T).FullName!),
            typeof(T),
            getRequestType,
            bufferSize,
            noOfPerformers,
            timeOut,
            requeueCount,
            requeueDelay,
            unacceptableMessageLimit,
            messagePumpType,
            channelFactory,
            emptyChannelDelay,
            channelFailureDelay,
            findTopicBy,
            findQueueBy,
            queueAttributes,
            topicAttributes,
            makeChannels)
    { }
}
