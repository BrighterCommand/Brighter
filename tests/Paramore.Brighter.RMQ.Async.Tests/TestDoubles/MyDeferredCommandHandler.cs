using Paramore.Brighter.Actions;

namespace Paramore.Brighter.RMQ.Async.Tests.TestDoubles;

internal sealed class MyDeferredCommandHandler : RequestHandler<MyDeferredCommand>
{
    public int HandledCount { get; set; } = 0;
    public override MyDeferredCommand Handle(MyDeferredCommand advanceTimerEvent)
    {
        //Just defer for ever
        throw new DeferMessageAction();
    }
}
