using System;

namespace Paramore.Brighter.Core.Tests.Observability.TestDoubles;

public class MyEventHandler : RequestHandler<MyEvent>
{
    public override MyEvent Handle(MyEvent command)
    {
        Console.WriteLine($"{command.Id} : {command.Name}");
        return base.Handle(command);
    }
}
