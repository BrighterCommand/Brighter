using System.Threading;
using Paramore.Brighter.Actions;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;

public class MyRejectedEventHandler : RequestHandler<MyRejectedEvent>
{
    private static readonly ManualResetEventSlim s_handled = new(false);

    public static void Reset() => s_handled.Reset();
    public static bool WaitForHandle(int timeoutMs = 5000) => s_handled.Wait(timeoutMs);

    public override MyRejectedEvent Handle(MyRejectedEvent myRejectedEvent)
    {
        s_handled.Set();
        throw new RejectMessageAction("Test of rejection flow");
    }
}
