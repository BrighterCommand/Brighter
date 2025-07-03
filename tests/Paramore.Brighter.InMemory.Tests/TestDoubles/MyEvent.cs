using System;

namespace Paramore.Brighter.InMemory.Tests.TestDoubles;

public class MyEvent() : Event(Guid.NewGuid())
{
    public string? Value { get; set; }
}
