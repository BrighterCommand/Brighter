using Paramore.Brighter;

namespace ParamoreBrighter.Quartz.Tests.TestDoubles;

public class MyEvent() : Event(Guid.NewGuid())
{
    public string Value { get; set; }
}
