using Paramore.Brighter.MessagingGateway.AWSSQS;

namespace Paramore.Brighter.MessageScheduler.Aws;

/// <summary>
/// The Aws message Scheduler factory
/// </summary>
public class AwsMessageSchedulerFactory(AWSMessagingGatewayConnection connection, string role, RoutingKey topicOrQueue)
    : IAmAMessageSchedulerFactory
{
    /// <summary>
    /// The AWS Scheduler group
    /// </summary>
    public SchedulerGroup Group { get; set; } = new();

    /// <summary>
    /// Get or create a scheduler id
    /// </summary>
    public Func<Message, string> GetOrCreateSchedulerId { get; set; } = _ => Guid.NewGuid().ToString("N");

    /// <summary>
    /// The flexible time window
    /// </summary>
    public int? FlexibleTimeWindowMinutes { get; set; }

    /// <summary>
    /// The topic or queue that Brighter should use for messaging scheduler
    /// It can be Topic Name/ARN or Queue Name/Url
    /// </summary>
    public RoutingKey TopicOrQueue { get; set; } = topicOrQueue;

    /// <summary>
    /// The AWS Role Name/ARN
    /// </summary>
    public string Role { get; set; } = role;

    /// <summary>
    /// Allow Brighter to give a priority to <see cref="MessageHeader.Topic"/> as destiny topic, in case it exists.
    /// </summary>
    public bool UseMessageTopicAsTarget { get; set; }
    
    /// <summary>
    /// Action to be performed when a conflict happen during scheduler creating
    /// </summary>
    public OnSchedulerConflict OnConflict { get; set; }

    public IAmAMessageScheduler Create(IAmACommandProcessor processor) 
        => new AwsMessageScheduler(new AWSClientFactory(connection), GetOrCreateSchedulerId,
            new Scheduler
            {
                Role = Role,
                TopicOrQueue = TopicOrQueue,
                UseMessageTopicAsTarget = UseMessageTopicAsTarget,
                OnConflict = OnConflict,
                FlexibleTimeWindowMinutes = FlexibleTimeWindowMinutes
            },
            Group);
}
