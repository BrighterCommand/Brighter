using Orders.Domain.Entities;
using Paramore.Brighter;

namespace Orders.Domain.Commands;

public class UpdateOrderCommand : Command
{
    public UpdateOrderCommand() : base(Guid.NewGuid())
    {
    }
    
    public int OrderId { get; init; }
    public OrderStatus Status { get; init; }
}
