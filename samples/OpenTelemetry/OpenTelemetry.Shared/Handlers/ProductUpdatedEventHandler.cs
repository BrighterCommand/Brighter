using OpenTelemetry.Shared.Events;
using Paramore.Brighter;
using Paramore.Brighter.Actions;

namespace OpenTelemetry.Shared.Handlers;

public class ProductUpdatedEventHandler : RequestHandler<ProductUpdatedEvent>
{
    private static Random _random = new Random();
    public override ProductUpdatedEvent Handle(ProductUpdatedEvent command)
    {
        Console.WriteLine($"Product updated to {command.Name} at {command.Date}");

        
        var num = _random.Next(0, 5);
        if (num < 4)
        {
            throw new DeferMessageAction();
        }
        
        return base.Handle(command);
    }
}
