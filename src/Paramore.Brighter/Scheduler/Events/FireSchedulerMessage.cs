using System;

namespace Paramore.Brighter.Scheduler.Events;

/// <summary>
/// A command to fire a scheduler message
/// </summary>
public class FireSchedulerMessage() : Command(Guid.NewGuid().ToString())
{
    /// <summary>
    /// The message that will be fire
    /// </summary>
    public Message Message { get; init; } = new();
    
    /// <summary>
    /// If it should post sync or async
    /// </summary>
    public bool Async { get; set; }
}
