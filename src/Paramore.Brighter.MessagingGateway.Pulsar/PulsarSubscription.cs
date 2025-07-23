using System;
using System.Buffers;
using DotPulsar;
using DotPulsar.Abstractions;

namespace Paramore.Brighter.MessagingGateway.Pulsar;

public class PulsarSubscription : Subscription
{
    public PulsarSubscription(Type dataType, SubscriptionName? subscriptionName = null, ChannelName? channelName = null, 
        RoutingKey? routingKey = null, int bufferSize = 1, int noOfPerformers = 1, TimeSpan? timeOut = null,
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
        : base(dataType, subscriptionName, channelName, routingKey, bufferSize, noOfPerformers, timeOut, requeueCount, 
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

    public ISchema<ReadOnlySequence<byte>> Schema { get; }
    
    public SubscriptionInitialPosition InitialPosition { get; }
    public int PriorityLevel { get; }
    public bool ReadCompacted { get; }
    
    public SubscriptionType SubscriptionType { get; }
    public bool AllowOutOfOrderDeliver { get; }
    
    public Action<IConsumerBuilder<ReadOnlySequence<byte>>>? Configuration { get; }
}

public class PulsarSubscription<T> : PulsarSubscription
    where T : IRequest
{
    public PulsarSubscription(SubscriptionName? subscriptionName = null, ChannelName? channelName = null,
        RoutingKey? routingKey = null, int bufferSize = 1, int noOfPerformers = 1, TimeSpan? timeOut = null,
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
        : base(typeof(T), subscriptionName, channelName, routingKey, bufferSize, noOfPerformers, timeOut, requeueCount,
            requeueDelay, unacceptableMessageLimit, messagePumpType, channelFactory, makeChannels, emptyChannelDelay,
            channelFailureDelay, schema, initialPosition, priorityLevel, readCompacted, subscriptionType, 
            allowOutOfOrderDeliver, configuration)
    {
        
    }
}
