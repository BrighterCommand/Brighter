using System.Collections.Concurrent;

namespace Paramore.Brighter.TickerQ.Tests.TestDoubles;

public class MyEventHandler(ConcurrentDictionary<string, string> receivedMessages) : RequestHandler<MyEventSync>
{
    public override MyEventSync Handle(MyEventSync myEvent)
    {
        receivedMessages[myEvent.Id] = nameof(MyEventHandler);
        return base.Handle(myEvent);
    }
}
