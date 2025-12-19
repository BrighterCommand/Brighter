namespace Paramore.Brighter.Hangfire.Tests.TestDoubles;

public class MyEventHandler(IDictionary<string, string> receivedMessages) : RequestHandler<MyEvent>
{
    public override MyEvent Handle(MyEvent advanceTimerEvent)
    {
        receivedMessages.Add(nameof(MyEventHandler), advanceTimerEvent.Id);
        return base.Handle(advanceTimerEvent);
    }
}
