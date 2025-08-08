using System;
using System.Buffers;
using DotPulsar;
using DotPulsar.Abstractions;

namespace Paramore.Brighter.MessagingGateway.Pulsar;

/// <summary>
/// Configuration for consuming messages from Apache Pulsar within the Brighter framework
/// </summary>
/// <remarks>
/// Extends Brighter's base Subscription with Pulsar-specific consumer configuration options.
/// Defines consumer behavior, message ordering, and subscription semantics.
/// </remarks>
public class PulsarSubscription : Subscription
{
    /// <summary>
    /// Initializes a new Pulsar subscription configuration
    /// </summary>
    /// <param name="subscriptionName">Unique name for this subscription</param>
    /// <param name="channelName">Name of the channel associated with this subscription</param>
    /// <param name="routingKey">Topic routing key</param>
    /// <param name="requestType">The <see cref="Type"/> of the data that this subscription handles.</param>
    /// <param name="getRequestType">The <see cref="Func{T,TResult}"/> that determines how we map a message to a type. Defaults to returning the <see cref="requestType"/> if null</param>
    /// <param name="bufferSize">Number of messages to prefetch</param>
    /// <param name="noOfPerformers">Number of concurrent consumers</param>
    /// <param name="timeOut">Timeout for receive operations</param>
    /// <param name="requeueCount">Number of times to requeue failed messages (-1 = infinite)</param>
    /// <param name="requeueDelay">Delay before requeuing failed messages</param>
    /// <param name="unacceptableMessageLimit">Limit for consecutive message failures before stopping</param>
    /// <param name="messagePumpType">Type of message pump to use</param>
    /// <param name="channelFactory">Factory for creating channels</param>
    /// <param name="makeChannels">Behavior when channels are missing</param>
    /// <param name="emptyChannelDelay">Delay when no messages are available</param>
    /// <param name="channelFailureDelay">Delay after channel failures</param>
    /// <param name="schema">Schema for message deserialization</param>
    /// <param name="initialPosition">Initial position in the topic</param>
    /// <param name="priorityLevel">Consumer priority level</param>
    /// <param name="readCompacted">Whether to read from compacted topics</param>
    /// <param name="subscriptionType">Pulsar subscription type</param>
    /// <param name="allowOutOfOrderDeliver">Allow out-of-order message delivery</param>
    /// <param name="configuration">Custom consumer configuration callback</param>
    public PulsarSubscription(SubscriptionName subscriptionName, ChannelName channelName, RoutingKey routingKey, 
        Type? requestType = null, Func<Message, Type>? getRequestType = null, int bufferSize = 1, int noOfPerformers = 1,
        TimeSpan? timeOut = null, int requeueCount = -1, TimeSpan? requeueDelay = null, int unacceptableMessageLimit = 0,
        MessagePumpType messagePumpType = MessagePumpType.Unknown, IAmAChannelFactory? channelFactory = null,
        OnMissingChannel makeChannels = OnMissingChannel.Create, TimeSpan? emptyChannelDelay = null, 
        TimeSpan? channelFailureDelay = null,
        ISchema<ReadOnlySequence<byte>>? schema = null,
        SubscriptionInitialPosition initialPosition = SubscriptionInitialPosition.Earliest, 
        int priorityLevel = 0, 
        bool readCompacted = false, 
        SubscriptionType subscriptionType = SubscriptionType.Exclusive, 
        bool allowOutOfOrderDeliver = false,
        Action<IConsumerBuilder<ReadOnlySequence<byte>>>? configuration = null) 
        : base(subscriptionName, channelName, routingKey, requestType, getRequestType, bufferSize, noOfPerformers, timeOut, requeueCount, 
            requeueDelay, unacceptableMessageLimit, messagePumpType, channelFactory, makeChannels, emptyChannelDelay, 
            channelFailureDelay)
    {
        Schema = schema ?? DotPulsar.Schema.ByteSequence;
        InitialPosition = initialPosition;
        PriorityLevel = priorityLevel;
        ReadCompacted = readCompacted;
        SubscriptionType = subscriptionType;
        AllowOutOfOrderDeliver = allowOutOfOrderDeliver;
        Configuration = configuration;
    }

    /// <summary>
    /// Gets the schema for message deserialization
    /// </summary>
    /// <value>
    /// Default: ByteSequence schema (raw byte array)
    /// </value>
    public ISchema<ReadOnlySequence<byte>> Schema { get; }
    
    /// <summary>
    /// Gets the initial position in the topic
    /// </summary>
    /// <value>
    /// Default: SubscriptionInitialPosition.Earliest
    /// </value>
    public SubscriptionInitialPosition InitialPosition { get; }
    
    /// <summary>
    /// Gets the consumer priority level
    /// </summary>
    /// <value>
    /// Default: 0
    /// </value>
    public int PriorityLevel { get; }
    
    /// <summary>
    /// Gets whether to read from compacted topics
    /// </summary>
    /// <value>
    /// Default: false
    /// </value>
    public bool ReadCompacted { get; }
    
    /// <summary>
    /// Gets the Pulsar subscription type
    /// </summary>
    /// <value>
    /// Default: SubscriptionType.Exclusive
    /// </value>
    public SubscriptionType SubscriptionType { get; }
    
    /// <summary>
    /// Gets whether to allow out-of-order message delivery
    /// </summary>
    /// <value>
    /// Default: false
    /// </value>
    public bool AllowOutOfOrderDeliver { get; }
    
    /// <summary>
    /// Gets an optional custom configuration callback
    /// </summary>
    /// <remarks>
    /// Allows direct access to the Pulsar consumer builder for advanced configuration
    /// not exposed through standard properties.
    /// </remarks>
    public Action<IConsumerBuilder<ReadOnlySequence<byte>>>? Configuration { get; }
}

/// <summary>
/// Typed subscription configuration for specific message types
/// </summary>
/// <typeparam name="T">The request type being consumed</typeparam>
/// <remarks>
/// Specializes <see cref="PulsarSubscription"/> for specific message types.
/// Automatically sets the DataType property to the specified generic type.
/// </remarks>
public class PulsarSubscription<T> : PulsarSubscription
    where T : IRequest
{
    /// <summary>
    /// Initializes a new typed Pulsar subscription
    /// </summary>
    /// <param name="subscriptionName">Unique name for this subscription</param>
    /// <param name="channelName">Name of the channel associated with this subscription</param>
    /// <param name="routingKey">Topic routing key</param>
    /// <param name="getRequestType">The <see cref="Func{T,TResult}"/> that determines how we map a message to a type. Defaults to returning the <see cref="T"/> if null</param>
    /// <param name="bufferSize">Number of messages to prefetch</param>
    /// <param name="noOfPerformers">Number of concurrent consumers</param>
    /// <param name="timeOut">Timeout for receive operations</param>
    /// <param name="requeueCount">Number of times to requeue failed messages (-1 = infinite)</param>
    /// <param name="requeueDelay">Delay before requeuing failed messages</param>
    /// <param name="unacceptableMessageLimit">Limit for consecutive message failures before stopping</param>
    /// <param name="messagePumpType">Type of message pump to use</param>
    /// <param name="channelFactory">Factory for creating channels</param>
    /// <param name="makeChannels">Behavior when channels are missing</param>
    /// <param name="emptyChannelDelay">Delay when no messages are available</param>
    /// <param name="channelFailureDelay">Delay after channel failures</param>
    /// <param name="schema">Schema for message deserialization</param>
    /// <param name="initialPosition">Initial position in the topic</param>
    /// <param name="priorityLevel">Consumer priority level</param>
    /// <param name="readCompacted">Whether to read from compacted topics</param>
    /// <param name="subscriptionType">Pulsar subscription type</param>
    /// <param name="allowOutOfOrderDeliver">Allow out-of-order message delivery</param>
    /// <param name="configuration">Custom consumer configuration callback</param>
    public PulsarSubscription(SubscriptionName subscriptionName, ChannelName channelName, RoutingKey routingKey,
        Func<Message, Type>? getRequestType = null, int bufferSize = 1, int noOfPerformers = 1, TimeSpan? timeOut = null,
        int requeueCount = -1, TimeSpan? requeueDelay = null, int unacceptableMessageLimit = 0,
        MessagePumpType messagePumpType = MessagePumpType.Unknown, IAmAChannelFactory? channelFactory = null,
        OnMissingChannel makeChannels = OnMissingChannel.Create, TimeSpan? emptyChannelDelay = null,
        TimeSpan? channelFailureDelay = null,
        ISchema<ReadOnlySequence<byte>>? schema = null,
        SubscriptionInitialPosition initialPosition = SubscriptionInitialPosition.Earliest,
        int priorityLevel = 0,
        bool readCompacted = false,
        SubscriptionType subscriptionType = SubscriptionType.Exclusive,
        bool allowOutOfOrderDeliver = false,
        Action<IConsumerBuilder<ReadOnlySequence<byte>>>? configuration = null)
        : base(subscriptionName, channelName, routingKey, typeof(T), getRequestType, bufferSize, noOfPerformers, timeOut, requeueCount,
            requeueDelay, unacceptableMessageLimit, messagePumpType, channelFactory, makeChannels, emptyChannelDelay,
            channelFailureDelay, schema, initialPosition, priorityLevel, readCompacted, subscriptionType, 
            allowOutOfOrderDeliver, configuration)
    {
        
    }
}
