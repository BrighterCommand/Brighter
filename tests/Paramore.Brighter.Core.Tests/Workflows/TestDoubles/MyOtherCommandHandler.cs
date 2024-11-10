using System.Collections.Generic;

namespace Paramore.Brighter.Core.Tests.Workflows.TestDoubles;

public class MyOtherCommandHandler(IAmACommandProcessor commandProcessor) : RequestHandler<MyOtherCommand>
{
    public static List<MyOtherCommand> ReceivedCommands { get; set; } = [];
    
    public override MyOtherCommand Handle(MyOtherCommand command)
    {
        LogCommand(command);
        commandProcessor?.Publish(new MyEvent(command.Value) {CorrelationId = command.CorrelationId});
        return base.Handle(command);
    }    
    
    private void LogCommand(MyOtherCommand request)
    {
        ReceivedCommands.Add(request);
    }

}
