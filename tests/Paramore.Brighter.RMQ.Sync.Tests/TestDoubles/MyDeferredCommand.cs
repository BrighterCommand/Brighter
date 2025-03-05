using System;

namespace Paramore.Brighter.RMQ.Sync.Tests.TestDoubles;

internal sealed class MyDeferredCommand : Command
{
    public string Value { get; set; }
    public MyDeferredCommand() : base(Guid.NewGuid()) { }
        
}
