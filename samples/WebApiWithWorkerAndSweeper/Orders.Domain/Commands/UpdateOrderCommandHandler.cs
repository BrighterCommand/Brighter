using Orders.Domain.Entities;
using Paramore.Brighter;
using Paramore.Brighter.Actions;

namespace Orders.Domain.Commands;

public class UpdateOrderCommandHandler : RequestHandlerAsync<UpdateOrderCommand>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IAmACommandProcessor _commandProcessor;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateOrderCommandHandler(IOrderRepository orderRepository, IAmACommandProcessor commandProcessor, IUnitOfWork unitOfWork)
    {
        _orderRepository = orderRepository;
        _commandProcessor = commandProcessor;
        _unitOfWork = unitOfWork;
    }

    public override async Task<UpdateOrderCommand> HandleAsync(UpdateOrderCommand command, CancellationToken cancellationToken = default(CancellationToken))
    {
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var order = await _orderRepository.GetOrderAsync(command.OrderId, cancellationToken);

            // ToDo: Add State change validation
            await order.UpdateStatusAsync(command.Status, _commandProcessor, cancellationToken);

            await _orderRepository.UpdateOrderAsync(order, cancellationToken);

            await _unitOfWork.CommitAsync(cancellationToken);
        }
        catch (Exception e)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            Console.WriteLine(e);
            throw new DeferMessageAction();
        }

        return await base.HandleAsync(command, cancellationToken);
    }
}
