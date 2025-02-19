using System;

namespace Paramore.Brighter.RMQ.Sync.Tests.TestDoubles;

internal class MyDeferredCommand : Command
{
    public string Value { get; set; }
    public MyDeferredCommand() : base(Guid.NewGuid()) { }
        
}
