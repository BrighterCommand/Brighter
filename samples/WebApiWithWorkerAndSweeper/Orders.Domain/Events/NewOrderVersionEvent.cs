using Paramore.Brighter;

namespace Orders.Domain.Events;

public class NewOrderVersionEvent : Event
{
    public const string Topic = "Orders.NewOrderVersionEvent";
    
    public NewOrderVersionEvent() : base(Guid.NewGuid())
    {
    }

    public string OrderId { get; init; } = string.Empty;
    public int Version { get; init; } 
    public string Number { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public bool ActionsPending { get; init; }
    public string Status { get; init; } = string.Empty;

}
