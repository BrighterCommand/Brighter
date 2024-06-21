using Microsoft.AspNetCore.Mvc;
using Orders.API.Requests;
using Orders.Domain.Commands;
using Paramore.Brighter;

namespace Orders.API.Controllers;

[Route("order")]
[ApiController]
public class OrdersControllers : ControllerBase
{
    private readonly IAmACommandProcessor _commandProcessor;
    
    public OrdersControllers(IAmACommandProcessor commandProcessor)
    {
        _commandProcessor = commandProcessor;
    }
    
    [HttpGet("{orderId}")]
    public IActionResult GetOrder(int orderId, CancellationToken cancellationToken)
    {
        return Ok();
    }

    [HttpPost("")]
    public async Task<IActionResult> CreateOrder([FromBody]CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateOrderCommand { Number = request.Number, Type = request.OrderType };
        
        await _commandProcessor.SendAsync(command, cancellationToken: cancellationToken);

        return Ok();
    }
    
    [HttpPatch("{orderId}")]
    public async Task<IActionResult> UpdateOrder([FromBody]UpdateOrderRequest request, int orderId, CancellationToken cancellationToken)
    {
        var command = new UpdateOrderCommand
        {
            OrderId = orderId,
            Status = request.Status
        };

        await _commandProcessor.SendAsync(command, cancellationToken: cancellationToken);

        return Ok();
    }
}
