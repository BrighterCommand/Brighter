using System;

namespace Paramore.Brighter.Scheduler.Events;

// TODO Add doc

public class SchedulerMessageFired() : Event(Guid.NewGuid().ToString())
{
    public Message Message { get; set; } = new();
}
