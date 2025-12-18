namespace Paramore.Brighter.MessageScheduler.AWS;

/// <summary>
/// Action to be performed about scheduler group
/// </summary>
public enum OnMissingSchedulerGroup
{
    /// <summary>
    /// Assume teh message group exists
    /// </summary>
    Assume,
    
    /// <summary>
    /// Check if the message group exists, if not create
    /// </summary>
    Create
}
