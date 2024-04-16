using Orders.Domain.Entities;

namespace Orders.API.Requests;

public class CreateOrderRequest
{
    public string Number { get; set; } = string.Empty;
    public OrderType OrderType { get; set; }
}
