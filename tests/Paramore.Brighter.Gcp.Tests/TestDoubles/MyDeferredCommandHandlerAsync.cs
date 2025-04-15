using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Actions;

namespace Paramore.Brighter.Gcp.Tests.TestDoubles;

internal sealed class MyDeferredCommandHandlerAsync : RequestHandlerAsync<MyDeferredCommand>
{
    public override async Task<MyDeferredCommand> HandleAsync(MyDeferredCommand command, CancellationToken cancellationToken = default)
    {
        // Simulate some asynchronous work
        await Task.Delay(100, cancellationToken);
        
        // Logic to handle the command
        if (command.Value == "Hello Redrive")
        {
            throw new DeferMessageAction();
        }

        return await base.HandleAsync(command, cancellationToken);
    }
}
