using Paramore.Brighter.Actions;

namespace Paramore.Brighter.AWS.Tests.TestDoubles
{
    internal sealed class MyDeferredCommandHandler : RequestHandler<MyDeferredCommand>
    {
        public int HandledCount { get; set; } = 0;
        public override MyDeferredCommand Handle(MyDeferredCommand command)
        {
            //Just defer for ever
            throw new DeferMessageAction();
        }
    }
}
