namespace Paramore.Brighter.TickerQ.Tests.TestDoubles;

public class MyEvent() : Event(Guid.NewGuid())
{
    public string Value { get; set; }
}
