using Orders.Domain.Entities;

namespace Orders.API.Requests;

public class CreateOrderRequest
{
    public string Number { get; set; }
    public OrderType OrderType { get; set; }
}
