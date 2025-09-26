using Amazon.Scheduler.Model;

namespace Paramore.Brighter.MessageScheduler.AWS;

/// <summary>
/// The AWS Scheduler group attributes
/// </summary>
public class SchedulerGroup
{
    /// <summary>
    /// The AWS scheduler group.
    /// </summary>
    public string Name { get; set; } = "default";

    /// <summary>
    /// The AWS scheduler group tags
    /// </summary>
    public List<Tag> Tags { get; set; } = [new() {Key = "Source", Value = "Brighter"}];
    
    /// <summary>
    /// The action to be performed during scheduler group creation
    /// </summary>
    public OnMissingSchedulerGroup MakeSchedulerGroup { get; set; }
}
