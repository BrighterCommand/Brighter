namespace Paramore.Brighter.TickerQ.Tests.TestDoubles;

public class MyEvent() : Event(Id.Random())
{
    public string Value { get; set; }
}
