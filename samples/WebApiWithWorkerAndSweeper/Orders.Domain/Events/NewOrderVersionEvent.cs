using Paramore.Brighter;

namespace Orders.Domain.Events;

public class NewOrderVersionEvent : Event
{
    public const string Topic = "Orders.NewOrderVersionEvent";

    public NewOrderVersionEvent() : base(Guid.NewGuid())
    {
    }

    public string OrderId { get; init; }
    public int Version { get; init; }
    public string Number { get; init; }
    public string Type { get; init; }
    public bool ActionsPending { get; init; }
    public string Status { get; init; }

}
