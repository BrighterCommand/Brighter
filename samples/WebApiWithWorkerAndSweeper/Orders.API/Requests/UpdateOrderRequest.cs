using Orders.Domain.Entities;

namespace Orders.API.Requests;

public class UpdateOrderRequest
{
    public OrderStatus Status { get; set; }
}
