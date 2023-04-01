using Paramore.Brighter;

namespace Orders.Domain.Events;

public class NewOrderVersionEventHandler : RequestHandlerAsync<NewOrderVersionEvent>
{
    public NewOrderVersionEventHandler()
    {
    }

    public override async Task<NewOrderVersionEvent> HandleAsync(NewOrderVersionEvent command, CancellationToken cancellationToken = default(CancellationToken))
    {
        // Todo: Plumb in action Logic
        var action = command.Type;

        return await base.HandleAsync(command, cancellationToken);
    }
}
