using Google.Cloud.PubSub.V1;
using Google.Protobuf.Collections;

namespace Paramore.Brighter.MessagingGateway.GcpPubSub;

public class PubSubSubscription : Subscription
{
    public string? ProjectId { get; set; }
    
    public TopicAttributes? TopicAttributes { get; set; }

    public int AckDeadlineSeconds { get; set; }
    public bool RetainAckedMessages { get; set; }
    
    public TimeSpan? MessageRetentionDuration { get; set; }

    public MapField<string, string> Labels { get; set; } = new();
    
    public bool EnableMessageOrdering { get; set; }
    
    public bool EnableExactlyOnceDelivery { get; set; }
    
    public CloudStorageConfig? Storage { get; set; }
    public ExpirationPolicy? ExpirationPolicy { get; set; }
    
    public DeadLetterPolicy? DeadLetterPolicy { get; set; }
    
    public PubSubSubscription(Type dataType, SubscriptionName? name = null, ChannelName? channelName = null, RoutingKey? routingKey = null, int bufferSize = 1, int noOfPerformers = 1, TimeSpan? timeOut = null, int requeueCount = -1, TimeSpan? requeueDelay = null, int unacceptableMessageLimit = 0, MessagePumpType messagePumpType = MessagePumpType.Unknown, IAmAChannelFactory? channelFactory = null, OnMissingChannel makeChannels = OnMissingChannel.Create, TimeSpan? emptyChannelDelay = null, TimeSpan? channelFailureDelay = null) : base(dataType, name, channelName, routingKey, bufferSize, noOfPerformers, timeOut, requeueCount, requeueDelay, unacceptableMessageLimit, messagePumpType, channelFactory, makeChannels, emptyChannelDelay, channelFailureDelay)
    {
    }
}
