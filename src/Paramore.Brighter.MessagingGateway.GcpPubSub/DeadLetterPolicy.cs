
namespace Paramore.Brighter.MessagingGateway.GcpPubSub;

public class DeadLetterPolicy(RoutingKey topic, ChannelName? subscriptionName = null)
{
    public RoutingKey TopicName { get; set; } = topic; 
    public TopicAttributes? TopicAttributes { get; set; }
    public ChannelName? SubscriptionName { get; set; } = subscriptionName;
    public int AckDeadlineSeconds { get; set; } = 60;
    public int MaxDeliveryAttempts { get; set; } = 10;
    
}
