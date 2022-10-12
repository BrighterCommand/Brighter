using OpenTelemetry.Shared.Commands;
using OpenTelemetry.Shared.Events;
using Paramore.Brighter;

namespace OpenTelemetry.Shared.Handlers;

public class UpdateProductCommandHandler : RequestHandler<UpdateProductCommand>
{
    private readonly IAmACommandProcessor _commandProcessor;

    public UpdateProductCommandHandler(IAmACommandProcessor commandProcessor)
    {
        _commandProcessor = commandProcessor;
    }
    public override UpdateProductCommand Handle(UpdateProductCommand command)
    {
        Console.WriteLine($"Product Updated to {command.Name}");
        var updateEvent = new ProductUpdatedEvent(command.Name, DateTime.Now);
        _commandProcessor.DepositPost(updateEvent);
        return base.Handle(command);
    }
}
