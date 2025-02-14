using Amazon.Scheduler;
using Amazon.Scheduler.Model;

namespace Paramore.Brighter.MessageScheduler.Aws;

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
    
    public RoutingKey MessageSchedulerTopic { get; set; } = RoutingKey.Empty;
    public RoutingKey RequestSchedulerTopic { get; set; } = RoutingKey.Empty;

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
