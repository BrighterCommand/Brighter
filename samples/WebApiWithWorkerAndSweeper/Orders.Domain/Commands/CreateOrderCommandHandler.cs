using Orders.Domain.Entities;
using Paramore.Brighter;

namespace Orders.Domain.Commands;

public class CreateOrderCommandHandler : RequestHandlerAsync<CreateOrderCommand>
{
    private readonly IOrderRepository _orderRepository;

    public CreateOrderCommandHandler(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public override async Task<CreateOrderCommand> HandleAsync(CreateOrderCommand command, CancellationToken cancellationToken = default(CancellationToken))
    {
        var order = new Order(0, command.Number, command.Type);

        await _orderRepository.CreateOrderAsync(order, cancellationToken);

        return await base.HandleAsync(command, cancellationToken);
    }
}
