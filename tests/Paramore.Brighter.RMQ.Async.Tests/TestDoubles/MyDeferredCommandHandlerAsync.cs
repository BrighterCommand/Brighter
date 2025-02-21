using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Actions;

namespace Paramore.Brighter.RMQ.Async.Tests.TestDoubles;

internal class MyDeferredCommandHandlerAsync : RequestHandlerAsync<MyDeferredCommand>
{
    public int HandledCount { get; set; } = 0;

    public override Task<MyDeferredCommand> HandleAsync(MyDeferredCommand command, CancellationToken cancellationToken = default)
    {
        // Just defer forever
        throw new DeferMessageAction();
    }
}
