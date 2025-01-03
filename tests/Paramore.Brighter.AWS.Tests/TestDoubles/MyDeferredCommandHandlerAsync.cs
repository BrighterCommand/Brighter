using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter;
using Paramore.Brighter.Actions;
using Paramore.Brighter.AWS.Tests.TestDoubles;

internal class MyDeferredCommandHandlerAsync : RequestHandlerAsync<MyDeferredCommand>
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
