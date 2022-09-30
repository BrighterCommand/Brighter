using OpenTelemetry.Shared.Events;
using Paramore.Brighter;

namespace OpenTelemetry.Shared.Handlers;

public class ProductUpdatedEventHandler : RequestHandler<ProductUpdatedEvent>
{
    public override ProductUpdatedEvent Handle(ProductUpdatedEvent command)
    {
        Console.WriteLine($"Product updated to {command.Name} at {command.Date}");
        return base.Handle(command);
    }
}
