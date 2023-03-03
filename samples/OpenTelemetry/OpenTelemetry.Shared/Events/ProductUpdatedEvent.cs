using Paramore.Brighter;

namespace OpenTelemetry.Shared.Events;

public class ProductUpdatedEvent : Event
{
    public ProductUpdatedEvent(string name, DateTime date) : base(Guid.NewGuid())
    {
        Name = name;
        Date = date;
    }

    public string Name { get; }
    public DateTime Date { get; }
}
