using Orders.Domain.Entities;

namespace Orders.Domain;

public interface IOrderRepository
{
    Task CreateOrderAsync(Order order, CancellationToken cancellationToken);
    Task UpdateOrderAsync(Order order, CancellationToken cancellationToken);
    Task<Order> GetOrderAsync(int orderId, CancellationToken cancellationToken);
}
