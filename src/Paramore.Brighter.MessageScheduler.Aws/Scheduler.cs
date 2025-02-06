using Amazon.Scheduler;
using Amazon.Scheduler.Model;

namespace Paramore.Brighter.MessageScheduler.Aws;

/// <summary>
/// The AWS scheduler attributes.
/// </summary>
public class Scheduler
{
    /// <summary>
    /// The AWS Role ARN
    /// </summary>
    public string Role { get; init; } = string.Empty;

    /// <summary>
    /// The flexible time window
    /// </summary>
    public int? FlexibleTimeWindowMinutes { get; init; }

    /// <summary>
    /// The topic ARN or Queue Url
    /// </summary>
    public RoutingKey TopicOrQueue { get; init; } = RoutingKey.Empty;
    
    /// <summary>
    /// Allow Brighter to give a priority to <see cref="MessageHeader.Topic"/> as destiny topic, in case it exists.
    /// </summary>
    public bool UseMessageTopicAsTarget { get; set; }
    
    /// <summary>
    /// Action to be performed when a conflict happen during scheduler creating
    /// </summary>
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
