namespace Paramore.Brighter.Hangfire.Tests.TestDoubles;

public class MyEventHandler(IDictionary<string, string> receivedMessages) : RequestHandler<MyEvent>
{
    public override MyEvent Handle(MyEvent myEvent)
    {
        receivedMessages.Add(nameof(MyEventHandler), myEvent.Id);
        return base.Handle(myEvent);
    }
}
