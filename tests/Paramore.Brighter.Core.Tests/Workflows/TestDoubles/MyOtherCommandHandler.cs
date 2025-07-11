using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Core.Tests.Workflows.TestDoubles;

public class MyOtherCommandHandlerAsync(IAmACommandProcessor commandProcessor) : RequestHandlerAsync<MyOtherCommand>
{
    public static List<MyOtherCommand> ReceivedCommands { get; set; } = [];

    public override async Task<MyOtherCommand> HandleAsync(MyOtherCommand command, CancellationToken cancellationToken = default)
    {
        LogCommand(command);
        commandProcessor?.PublishAsync(new MyEvent(command.Value) {CorrelationId = command.CorrelationId}, cancellationToken: cancellationToken);
        return await base.HandleAsync(command, cancellationToken);
    }    
    
    private void LogCommand(MyOtherCommand request)
    {
        ReceivedCommands.Add(request);
    }

}
