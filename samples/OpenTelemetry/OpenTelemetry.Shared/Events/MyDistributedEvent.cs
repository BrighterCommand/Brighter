using Paramore.Brighter;

namespace OpenTelemetry.Shared.Events;

public class MyDistributedEvent : Event
{
    public MyDistributedEvent(string name) : base(Guid.NewGuid())
    {
        Name = name;
    }

    public string Name { get; }
}
