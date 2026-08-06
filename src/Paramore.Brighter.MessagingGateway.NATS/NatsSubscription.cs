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
using NATS.Client.Core;

namespace Paramore.Brighter.MessagingGateway.NATS;

/// <summary>
/// Subscription configuration for consuming from a core NATS subject.
/// </summary>
/// <remarks>
/// Core NATS is at-most-once; for redelivery and acknowledgement semantics use
/// <see cref="NatsStreamSubscription"/> instead. The channel name is used as the NATS subject to subscribe to.
/// </remarks>
public class NatsSubscription : Subscription, IUseBrighterDeadLetterSupport, IUseBrighterInvalidMessageSupport
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NatsSubscription"/> class.
    /// </summary>
    /// <param name="subscriptionName">The <see cref="MessagingGateway.SubscriptionName"/> of the subscription.</param>
    /// <param name="channelName">The <see cref="MessagingGateway.ChannelName"/>; used as the NATS subject to subscribe to.</param>
    /// <param name="routingKey">The <see cref="MessagingGateway.RoutingKey"/>; the topic messages are published under.</param>
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
    /// <param name="queueGroup">An optional NATS queue group name; subscribers in the same queue group load-balance messages. Optional; defaults to <see langword="null"/>, giving an ungrouped subscription that receives every message.</param>
    /// <param name="natsSubOpts">Optional <see cref="NATS.Client.Core.NatsSubOpts"/> to fine-tune the underlying NATS subscription.</param>
    public NatsSubscription(SubscriptionName subscriptionName,
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
        MessagePumpType messagePumpType = MessagePumpType.Unknown,
        IAmAChannelFactory? channelFactory = null,
        OnMissingChannel makeChannels = OnMissingChannel.Create,
        TimeSpan? emptyChannelDelay = null,
        TimeSpan? channelFailureDelay = null,
        TimeSpan? unacceptableMessageLimitWindow = null,
        string? queueGroup = null,
        NatsSubOpts? natsSubOpts = null)
        : base(subscriptionName, channelName, routingKey, requestType,
            getRequestType, bufferSize, noOfPerformers, timeOut,
            requeueCount, requeueDelay, unacceptableMessageLimit,
            messagePumpType, channelFactory, makeChannels,
            emptyChannelDelay, channelFailureDelay,
            unacceptableMessageLimitWindow)
    {
        QueueGroup = queueGroup;
        NatsSubOpts = natsSubOpts;
    }

    /// <summary>
    /// Gets the NATS queue group name for the subscription.
    /// </summary>
    /// <value>The queue group name as a <see cref="string"/>, or <see langword="null"/> for an ungrouped subscription.</value>
    public string? QueueGroup { get; }

    /// <summary>
    /// Gets the low-level options for the underlying NATS subscription.
    /// </summary>
    /// <value>The <see cref="NATS.Client.Core.NatsSubOpts"/>, or <see langword="null"/> to use the client defaults.</value>
    public NatsSubOpts? NatsSubOpts { get; }

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
/// Subscription configuration for consuming from a core NATS subject, typed to a specific request.
/// </summary>
/// <remarks>
/// Defaults the subscription name, channel name (the NATS subject), and routing key to the full name of
/// <typeparamref name="TRequest"/>, which is a valid subject for core NATS.
/// </remarks>
/// <typeparam name="TRequest">The type of request that this subscription handles; must implement <see cref="IRequest"/>.</typeparam>
public class NatsSubscription<TRequest> : NatsSubscription
    where TRequest : class, IRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NatsSubscription{TRequest}"/> class.
    /// </summary>
    /// <param name="subscriptionName">The <see cref="MessagingGateway.SubscriptionName"/> of the subscription. Optional; defaults to the full name of <typeparamref name="TRequest"/>.</param>
    /// <param name="channelName">The <see cref="MessagingGateway.ChannelName"/>; used as the NATS subject to subscribe to. Optional; defaults to the full name of <typeparamref name="TRequest"/>.</param>
    /// <param name="routingKey">The <see cref="MessagingGateway.RoutingKey"/>; the topic messages are published under. Optional; defaults to the full name of <typeparamref name="TRequest"/>.</param>
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
    /// <param name="queueGroup">An optional NATS queue group name; subscribers in the same queue group load-balance messages. Optional; defaults to <see langword="null"/>, giving an ungrouped subscription that receives every message.</param>
    /// <param name="natsSubOpts">Optional <see cref="NATS.Client.Core.NatsSubOpts"/> to fine-tune the underlying NATS subscription.</param>
    public NatsSubscription(
        SubscriptionName? subscriptionName = null,
        ChannelName? channelName = null,
        RoutingKey? routingKey = null,
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
        string? queueGroup = null,
        NatsSubOpts? natsSubOpts = null)
        : base(subscriptionName ?? new SubscriptionName(typeof(TRequest).FullName!),
            channelName ?? new ChannelName(typeof(TRequest).FullName!),
            routingKey ?? new RoutingKey(typeof(TRequest).FullName!),
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
            queueGroup,
            natsSubOpts)
    {
    }
}
