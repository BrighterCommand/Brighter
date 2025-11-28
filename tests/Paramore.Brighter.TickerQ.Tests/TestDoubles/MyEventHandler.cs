namespace Paramore.Brighter.TickerQ.Tests.TestDoubles;

public class MyEventHandler(IDictionary<string, string> receivedMessages) : RequestHandler<MyEvent>
{
    public override MyEvent Handle(MyEvent command)
    {
        receivedMessages.Add(nameof(MyEventHandler), command.Id);
        return base.Handle(command);
    }
}
