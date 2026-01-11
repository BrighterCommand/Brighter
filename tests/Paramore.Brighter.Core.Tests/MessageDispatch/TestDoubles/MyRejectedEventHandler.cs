using Paramore.Brighter.Actions;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;

public class MyRejectedEventHandler : RequestHandler<MyRejectedEvent>
{
    public override MyRejectedEvent Handle(MyRejectedEvent myRejectedEvent)
    {
        throw new RejectMessageAction("Test of rejection flow");
    }
}
