
namespace Paramore.Brighter.MessagingGateway.GcpPubSub;

public class DeadLetterPolicy(RoutingKey topic, ChannelName subscription)
{
    public RoutingKey TopicName { get; set; } = topic; 
    public ChannelName Subscription { get; set; } = subscription;
    public string? PublisherMember { get; set; }
    public string? SubscriberMember { get; set; }
    
    public int AckDeadlineSeconds { get; set; } = 60;
    public int MaxDeliveryAttempts { get; set; } = 10;
    
}
