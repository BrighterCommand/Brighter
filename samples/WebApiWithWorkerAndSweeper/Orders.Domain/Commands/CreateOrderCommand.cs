using Orders.Domain.Entities;
using Paramore.Brighter;

namespace Orders.Domain.Commands;

public class CreateOrderCommand : Command
{
    public CreateOrderCommand() : base(Guid.NewGuid())
    {
    }

    public string Number { get; init; } = string.Empty;
    public OrderType Type { get; init; } = OrderType.Delivery;
}
