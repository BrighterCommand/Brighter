using Amazon.Scheduler;
using Amazon.Scheduler.Model;

namespace Paramore.Brighter.MessageScheduler.AWS;

/// <summary>
/// The AWS scheduler attributes.
/// </summary>
public class Scheduler
{
    /// <summary>
    /// The role ARN
    /// </summary>
    public string RoleArn { get; init; } = string.Empty;

    /// <summary>
    /// The flexible time window
    /// </summary>
    public int? FlexibleTimeWindowMinutes { get; init; }
    
    /// <summary>
    /// The scheduler message topic
    /// </summary>
    /// <remarks>
    /// It can be SNS name/ARN or SQS Name/Url
    /// </remarks>
    public RoutingKey SchedulerTopic { get; set; } = RoutingKey.Empty;

    public bool UseMessageTopicAsTarget { get; set; }

    public OnSchedulerConflict OnConflict { get; init; }

    internal FlexibleTimeWindow ToFlexibleTimeWindow()
    {
        return new FlexibleTimeWindow
        {
            Mode = FlexibleTimeWindowMinutes == null ? FlexibleTimeWindowMode.OFF : FlexibleTimeWindowMode.FLEXIBLE,
            MaximumWindowInMinutes = FlexibleTimeWindowMinutes
        };
    }
}
