using Orders.Domain.Events;
using Paramore.Brighter;

namespace Orders.Domain.Entities;

public class Order
{
    public long Id { get; private set; }
    public int Version { get; private set; }
    public string Number { get; private set; }
    public OrderType Type { get; private set; }
    public bool ActionsPending { get; private set; }
    public OrderStatus Status { get; private set; }

    public Order(long id, string number, OrderType type, bool actionsPending = false, OrderStatus status = OrderStatus.Created, int version = 1)
    {
        Id = id;
        Number = number;
        Type = type;
        ActionsPending = actionsPending;
        Status = status;
        Version = version;
    }

    public async Task UpdateStatusAsync(OrderStatus newStatus, IAmACommandProcessor commandProcessor, CancellationToken cancellationToken)
    {
        Status = newStatus;
        Version++;

        var updatedEvent = new NewOrderVersionEvent
        {
            OrderId = this.Id.ToString(),
            Version = this.Version,
            Number = this.Number,
            ActionsPending = this.ActionsPending,
            Status = this.Status.ToString(),
            Type = this.Type.ToString()
        };

        await commandProcessor.DepositPostAsync(updatedEvent, cancellationToken: cancellationToken);
    }
}
