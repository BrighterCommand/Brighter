using System.Diagnostics;
using OpenTelemetry.Shared.Events;
using Paramore.Brighter;

namespace OpenTelemetry.Shared.Handlers;

public class MyDistributedEventHandler : RequestHandler<MyDistributedEvent>
{
    public override MyDistributedEvent Handle(MyDistributedEvent command)
    {
        Console.WriteLine($"Id: {command.Id} {Environment.NewLine}Messge: {command.Name}{Environment.NewLine}");
        Context.Span.AddEvent(new ActivityEvent("Did a thing"));
        
        return base.Handle(command);
    }
}
