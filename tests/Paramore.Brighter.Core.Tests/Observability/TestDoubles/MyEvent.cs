using System;
using System.Runtime.CompilerServices;

namespace Paramore.Brighter.Core.Tests.Observability.TestDoubles;

public class MyEvent : Event
{
    public const string Topic = "MyTopic";
        
    public MyEvent(Guid id) : base(id)
    {
    }

    public MyEvent(string name): base(Guid.NewGuid())
    {
        Name = name;
    } 
    
    public string Name { get; set; }
}
