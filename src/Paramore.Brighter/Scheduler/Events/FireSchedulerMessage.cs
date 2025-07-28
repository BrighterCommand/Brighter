namespace Paramore.Brighter.Scheduler.Events;

/// <summary>
/// A command to fire a scheduler message
/// </summary>
public class FireSchedulerMessage() : Command(Id.Random)
{
    /// <summary>
    /// The message that will be fire
    /// </summary>
    public Message Message { get; set; } = new();
    
    /// <summary>
    /// If it should post sync or async
    /// </summary>
    public bool Async { get; set; }
}
