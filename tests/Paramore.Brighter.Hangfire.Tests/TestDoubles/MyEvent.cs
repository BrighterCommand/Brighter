namespace Paramore.Brighter.Hangfire.Tests.TestDoubles;

public class MyEvent() : Event(Guid.NewGuid())
{
    public string Value { get; set; }
}
