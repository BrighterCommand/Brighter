using System;

namespace Paramore.Brighter.Scheduler.Events;

/// <summary>
/// The command to fire a scheduler request
/// </summary>
public class FireSchedulerRequest() : Command(Id.Random)
{
    /// <summary>
    /// The <see cref="RequestSchedulerType"/>
    /// </summary>
    public RequestSchedulerType SchedulerType { get; set; }

    /// <summary>
    /// The request type
    /// </summary>
    public string RequestType { get; set; } = string.Empty;

    /// <summary>
    /// The request data
    /// </summary>
    public string RequestData { get; set; } = string.Empty;
    
    /// <summary>
    /// Flag indicating if the flow should be sync or async
    /// </summary>
    public bool Async { get; set; }
}
