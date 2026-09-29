using System;
using System.Threading.Tasks;
using Paramore.Brighter.Actions;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;

public class MyRejectedEventHandler : RequestHandler<MyRejectedEvent>
{
    private static TaskCompletionSource<bool> s_handled = NewCompletionSource();

    public static void Reset() => s_handled = NewCompletionSource();

    public static Task<bool> WaitForHandleAsync(int timeoutMs = 5000) =>
        s_handled.Task.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs));

    private static TaskCompletionSource<bool> NewCompletionSource() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public override MyRejectedEvent Handle(MyRejectedEvent myRejectedEvent)
    {
        s_handled.TrySetResult(true);
        throw new RejectMessageAction("Test of rejection flow");
    }
}
