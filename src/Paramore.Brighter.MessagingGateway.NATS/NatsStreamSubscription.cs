#region Licence

/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;

namespace Paramore.Brighter.MessagingGateway.NATS;

/// <summary>
/// Subscription configuration for consuming from a NATS JetStream stream with at-least-once delivery.
/// </summary>
/// <remarks>
/// The channel name is used as the JetStream stream name and the routing key as the stream subject when the
/// stream is created; <see cref="Consumer"/> names the durable consumer within the stream. Use
/// <see cref="StreamConfiguration"/> and <see cref="ConsumerOption"/> to take full control of the JetStream
/// resources created when <see cref="Subscription.MakeChannels"/> allows creation.
/// </remarks>
public class NatsStreamSubscription : Subscription, IUseBrighterDeadLetterSupport, IUseBrighterInvalidMessageSupport
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NatsStreamSubscription"/> class.
    /// </summary>
    /// <param name="subscriptionName">The <see cref="MessagingGateway.SubscriptionName"/> of the subscription.</param>
    /// <param name="channelName">The <see cref="MessagingGateway.ChannelName"/>; used as the JetStream stream name.</param>
    /// <param name="routingKey">The <see cref="MessagingGateway.RoutingKey"/>; used as the subject the stream subscribes to.</param>
    /// <param name="consumer">The name of the JetStream consumer; used as both the consumer name and durable name unless <paramref name="consumerOption"/> says otherwise.</param>
    /// <param name="requestType">The <see cref="Type"/> of the request carried by messages, if fixed.</param>
    /// <param name="getRequestType">A <see cref="Func{T,TResult}"/> that resolves the request <see cref="Type"/> from a <see cref="Message"/>, if it varies.</param>
    /// <param name="bufferSize">The number of messages to buffer for the channel; defaults to 1.</param>
    /// <param name="noOfPerformers">The number of threads pumping this channel; defaults to 1.</param>
    /// <param name="timeOut">The <see cref="TimeSpan"/> to wait for a message before treating the channel as empty.</param>
    /// <param name="requeueCount">How many times a message is requeued before it is rejected; -1 for unlimited.</param>
    /// <param name="requeueDelay">The <see cref="TimeSpan"/> to wait before requeuing a failed message.</param>
    /// <param name="unacceptableMessageLimit">How many unacceptable messages to tolerate before stopping the channel; 0 for unlimited.</param>
    /// <param name="messagePumpType">The <see cref="MessagePumpType"/>; whether the pump reads messages synchronously or asynchronously.</param>
    /// <param name="channelFactory">An optional <see cref="IAmAChannelFactory"/> used to create the channel infrastructure.</param>
    /// <param name="makeChannels">The <see cref="OnMissingChannel"/> policy for missing infrastructure.</param>
    /// <param name="emptyChannelDelay">The <see cref="TimeSpan"/> to wait when the channel is empty.</param>
    /// <param name="channelFailureDelay">The <see cref="TimeSpan"/> to wait when the channel fails.</param>
    /// <param name="unacceptableMessageLimitWindow">The <see cref="TimeSpan"/> window the unacceptable message limit applies to.</param>
    /// <param name="ordered">Whether to consume in strict order via a JetStream ordered consumer; defaults to <see langword="false"/>.</param>
    /// <param name="orderedConsumerOption">Optional <see cref="NatsJSOrderedConsumerOpts"/> for the ordered consumer; only used when <paramref name="ordered"/> is <see langword="true"/>.</param>
    /// <param name="idleHeartbeat">Optional <see cref="TimeSpan"/> idle heartbeat for the consume loop.</param>
    /// <param name="priorityGroup">Optional <see cref="NatsJSPriorityGroupOpts"/> for prioritized pull consumption.</param>
    /// <param name="streamConfiguration">Optional explicit <see cref="StreamConfig"/> used to create the stream; when omitted, a stream named after <paramref name="channelName"/> subscribing <paramref name="routingKey"/> is created.</param>
    /// <param name="consumerOption">Optional explicit <see cref="ConsumerConfig"/> used to create the consumer; when omitted, a durable consumer named after <paramref name="consumer"/> is created.</param>
    /// <param name="deadLetterRoutingKey">Optional <see cref="MessagingGateway.RoutingKey"/> of the dead-letter channel for rejected messages.</param>
    /// <param name="invalidMessageRoutingKey">Optional <see cref="MessagingGateway.RoutingKey"/> of the channel for messages that cannot be deserialized.</param>
    public NatsStreamSubscription(
        SubscriptionName subscriptionName,
        ChannelName channelName,
        RoutingKey routingKey,
        string consumer,
        Type? requestType = null,
        Func<Message, Type>? getRequestType = null,
        int bufferSize = 1, int noOfPerformers = 1, TimeSpan? timeOut = null,
        int requeueCount = -1, TimeSpan? requeueDelay = null, int unacceptableMessageLimit = 0,
        MessagePumpType messagePumpType = MessagePumpType.Unknown, IAmAChannelFactory? channelFactory = null,
        OnMissingChannel makeChannels = OnMissingChannel.Create, TimeSpan? emptyChannelDelay = null,
        TimeSpan? channelFailureDelay = null, TimeSpan? unacceptableMessageLimitWindow = null,
        bool ordered = false,
        NatsJSOrderedConsumerOpts? orderedConsumerOption = null,
        TimeSpan? idleHeartbeat = null,
        NatsJSPriorityGroupOpts? priorityGroup = null,
        StreamConfig? streamConfiguration = null,
        ConsumerConfig? consumerOption = null,
        RoutingKey? deadLetterRoutingKey = null,
        RoutingKey? invalidMessageRoutingKey = null)
        : base(subscriptionName, channelName, routingKey, requestType, getRequestType, bufferSize, noOfPerformers,
            timeOut, requeueCount, requeueDelay, unacceptableMessageLimit, messagePumpType, channelFactory,
            makeChannels, emptyChannelDelay, channelFailureDelay, unacceptableMessageLimitWindow)
    {
        Consumer = consumer;
        Ordered = ordered;
        OrderedConsumerOption = orderedConsumerOption;
        IdleHeartbeat = idleHeartbeat;
        PriorityGroup = priorityGroup ?? new NatsJSPriorityGroupOpts();
        StreamConfiguration = streamConfiguration;
        ConsumerOption = consumerOption ?? new ConsumerConfig { Name = consumer, DurableName = consumer };
        DeadLetterRoutingKey = deadLetterRoutingKey;
        InvalidMessageRoutingKey = invalidMessageRoutingKey;
    }

    /// <summary>
    /// Gets the name of the JetStream consumer within the stream.
    /// </summary>
    /// <value>The consumer name as a <see cref="string"/>.</value>
    public string Consumer { get; }

    /// <summary>
    /// Gets a value indicating whether messages are consumed in strict order via a JetStream ordered consumer.
    /// </summary>
    /// <value><see langword="true"/> when an ordered consumer is used; otherwise <see langword="false"/>.</value>
    public bool Ordered { get; }

    /// <summary>
    /// Gets the options for the ordered consumer.
    /// </summary>
    /// <value>The <see cref="NatsJSOrderedConsumerOpts"/>, or <see langword="null"/> to use the client defaults; only used when <see cref="Ordered"/> is <see langword="true"/>.</value>
    public NatsJSOrderedConsumerOpts? OrderedConsumerOption { get; }

    /// <summary>
    /// Gets the idle heartbeat for the consume loop.
    /// </summary>
    /// <value>The idle heartbeat <see cref="TimeSpan"/>, or <see langword="null"/> to use the client default.</value>
    public TimeSpan? IdleHeartbeat { get; }

    /// <summary>
    /// Gets the priority group options for prioritized pull consumption.
    /// </summary>
    /// <value>The <see cref="NatsJSPriorityGroupOpts"/>; never <see langword="null"/>.</value>
    public NatsJSPriorityGroupOpts PriorityGroup { get; }

    /// <summary>
    /// Gets the explicit stream configuration used when the stream is created.
    /// </summary>
    /// <value>The <see cref="StreamConfig"/>, or <see langword="null"/> to derive one from the channel name and routing key.</value>
    public StreamConfig? StreamConfiguration { get; }

    /// <summary>
    /// Gets the consumer configuration used when the consumer is created.
    /// </summary>
    /// <value>The <see cref="ConsumerConfig"/>; defaults to a durable consumer named after <see cref="Consumer"/>.</value>
    public ConsumerConfig ConsumerOption { get; }

    /// <summary>
    /// Gets or sets the routing key of the dead-letter channel for rejected messages.
    /// </summary>
    /// <value>The dead-letter <see cref="MessagingGateway.RoutingKey"/>, or <see langword="null"/> when dead-lettering is not configured.</value>
    public RoutingKey? DeadLetterRoutingKey { get; set; }

    /// <summary>
    /// Gets or sets the routing key of the channel for messages that cannot be deserialized.
    /// </summary>
    /// <value>The invalid-message <see cref="MessagingGateway.RoutingKey"/>, or <see langword="null"/> when not configured.</value>
    public RoutingKey? InvalidMessageRoutingKey { get; set; }
}

/// <summary>
/// Subscription configuration for consuming from a NATS JetStream stream, typed to a specific request.
/// </summary>
/// <remarks>
/// Defaults the subscription name and routing key (the stream subject) to the full name of
/// <typeparamref name="TRequest"/>. Because JetStream stream and durable consumer names cannot contain
/// '.', whitespace, or wildcards, the channel name (the stream name) and consumer name default to the
/// full name with other characters replaced by '-' — the same rule <see cref="NatsStreamPublication"/>
/// uses, so a generic publication and a generic subscription of the same request type meet on one stream.
/// </remarks>
/// <typeparam name="TRequest">The type of request that this subscription handles; must implement <see cref="IRequest"/>.</typeparam>
public class NatsStreamSubscription<TRequest> : NatsStreamSubscription
    where TRequest : class, IRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NatsStreamSubscription{TRequest}"/> class.
    /// </summary>
    /// <param name="subscriptionName">The <see cref="MessagingGateway.SubscriptionName"/> of the subscription. Optional; defaults to the full name of <typeparamref name="TRequest"/>.</param>
    /// <param name="channelName">The <see cref="MessagingGateway.ChannelName"/>; used as the JetStream stream name. Optional; defaults to the full name of <typeparamref name="TRequest"/> with characters invalid in a stream name replaced by '-'.</param>
    /// <param name="routingKey">The <see cref="MessagingGateway.RoutingKey"/>; used as the subject the stream subscribes to. Optional; defaults to the full name of <typeparamref name="TRequest"/>.</param>
    /// <param name="consumer">The name of the JetStream consumer; used as both the consumer name and durable name unless <paramref name="consumerOption"/> says otherwise. Optional; defaults to the full name of <typeparamref name="TRequest"/> with characters invalid in a consumer name replaced by '-'.</param>
    /// <param name="getRequestType">A <see cref="Func{T,TResult}"/> that resolves the request <see cref="Type"/> from a <see cref="Message"/>; optional, defaults to always returning <typeparamref name="TRequest"/>.</param>
    /// <param name="bufferSize">The number of messages to buffer for the channel; defaults to 1.</param>
    /// <param name="noOfPerformers">The number of threads pumping this channel; defaults to 1.</param>
    /// <param name="timeOut">The <see cref="TimeSpan"/> to wait for a message before treating the channel as empty.</param>
    /// <param name="requeueCount">How many times a message is requeued before it is rejected; -1 for unlimited.</param>
    /// <param name="requeueDelay">The <see cref="TimeSpan"/> to wait before requeuing a failed message.</param>
    /// <param name="unacceptableMessageLimit">How many unacceptable messages to tolerate before stopping the channel; 0 for unlimited.</param>
    /// <param name="messagePumpType">The <see cref="MessagePumpType"/>; whether the pump reads messages synchronously or asynchronously.</param>
    /// <param name="channelFactory">An optional <see cref="IAmAChannelFactory"/> used to create the channel infrastructure.</param>
    /// <param name="makeChannels">The <see cref="OnMissingChannel"/> policy for missing infrastructure.</param>
    /// <param name="emptyChannelDelay">The <see cref="TimeSpan"/> to wait when the channel is empty.</param>
    /// <param name="channelFailureDelay">The <see cref="TimeSpan"/> to wait when the channel fails.</param>
    /// <param name="unacceptableMessageLimitWindow">The <see cref="TimeSpan"/> window the unacceptable message limit applies to.</param>
    /// <param name="ordered">Whether to consume in strict order via a JetStream ordered consumer; defaults to <see langword="false"/>.</param>
    /// <param name="orderedConsumerOption">Optional <see cref="NatsJSOrderedConsumerOpts"/> for the ordered consumer; only used when <paramref name="ordered"/> is <see langword="true"/>.</param>
    /// <param name="idleHeartbeat">Optional <see cref="TimeSpan"/> idle heartbeat for the consume loop.</param>
    /// <param name="priorityGroup">Optional <see cref="NatsJSPriorityGroupOpts"/> for prioritized pull consumption.</param>
    /// <param name="streamConfiguration">Optional explicit <see cref="StreamConfig"/> used to create the stream; when omitted, a stream named after <paramref name="channelName"/> subscribing <paramref name="routingKey"/> is created.</param>
    /// <param name="consumerOption">Optional explicit <see cref="ConsumerConfig"/> used to create the consumer; when omitted, a durable consumer named after <paramref name="consumer"/> is created.</param>
    /// <param name="deadLetterRoutingKey">Optional <see cref="MessagingGateway.RoutingKey"/> of the dead-letter channel for rejected messages.</param>
    /// <param name="invalidMessageRoutingKey">Optional <see cref="MessagingGateway.RoutingKey"/> of the channel for messages that cannot be deserialized.</param>
    public NatsStreamSubscription(
        SubscriptionName? subscriptionName = null,
        ChannelName? channelName = null,
        RoutingKey? routingKey = null,
        string? consumer = null,
        Func<Message, Type>? getRequestType = null,
        int bufferSize = 1,
        int noOfPerformers = 1,
        TimeSpan? timeOut = null,
        int requeueCount = -1,
        TimeSpan? requeueDelay = null,
        int unacceptableMessageLimit = 0,
        MessagePumpType messagePumpType = MessagePumpType.Unknown,
        IAmAChannelFactory? channelFactory = null,
        OnMissingChannel makeChannels = OnMissingChannel.Create,
        TimeSpan? emptyChannelDelay = null,
        TimeSpan? channelFailureDelay = null,
        TimeSpan? unacceptableMessageLimitWindow = null,
        bool ordered = false,
        NatsJSOrderedConsumerOpts? orderedConsumerOption = null,
        TimeSpan? idleHeartbeat = null,
        NatsJSPriorityGroupOpts? priorityGroup = null,
        StreamConfig? streamConfiguration = null,
        ConsumerConfig? consumerOption = null,
        RoutingKey? deadLetterRoutingKey = null,
        RoutingKey? invalidMessageRoutingKey = null)
        : base(subscriptionName ?? new SubscriptionName(typeof(TRequest).FullName!),
            channelName ?? new ChannelName(NatsNameSanitizer.Sanitize(typeof(TRequest).FullName!)),
            routingKey ?? new RoutingKey(typeof(TRequest).FullName!),
            consumer ?? NatsNameSanitizer.Sanitize(typeof(TRequest).FullName!),
            typeof(TRequest),
            getRequestType,
            bufferSize,
            noOfPerformers,
            timeOut,
            requeueCount,
            requeueDelay,
            unacceptableMessageLimit,
            messagePumpType,
            channelFactory,
            makeChannels,
            emptyChannelDelay,
            channelFailureDelay,
            unacceptableMessageLimitWindow,
            ordered,
            orderedConsumerOption,
            idleHeartbeat,
            priorityGroup,
            streamConfiguration,
            consumerOption,
            deadLetterRoutingKey,
            invalidMessageRoutingKey)
    {
    }
}
