using System;

namespace Paramore.Brighter.RMQ.Async.Tests.TestDoubles;

internal sealed class MyDeferredCommand : Command
{
    public string Value { get; set; }
    public MyDeferredCommand() : base(Guid.NewGuid()) { }
}
