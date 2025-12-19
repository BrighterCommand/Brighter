using Paramore.Brighter.Actions;

namespace Paramore.Brighter.RocketMQ.Tests.TestDoubles;

internal class MyDeferredCommandHandler : RequestHandler<MyDeferredCommand>
{
    public int HandledCount { get; set; } = 0;
    public override MyDeferredCommand Handle(MyDeferredCommand advanceTimerEvent)
    {
        //Just defer for ever
        throw new DeferMessageAction();
    }
}
