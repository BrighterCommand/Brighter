using System;

namespace Paramore.Brighter.InMemory.Tests.TestDoubles;

public class MyEvent() : Event(Guid.NewGuid().ToString())
{
    public string? Value { get; set; }
}
