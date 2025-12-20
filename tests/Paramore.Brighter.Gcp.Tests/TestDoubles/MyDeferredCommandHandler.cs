using Paramore.Brighter.Actions;

namespace Paramore.Brighter.Gcp.Tests.TestDoubles;

internal sealed class MyDeferredCommandHandler : RequestHandler<MyDeferredCommand>
{
    public int HandledCount { get; set; } = 0;
    public override MyDeferredCommand Handle(MyDeferredCommand myDeferredCommand)
    {
        //Just defer for ever
        throw new DeferMessageAction();
    }
}
