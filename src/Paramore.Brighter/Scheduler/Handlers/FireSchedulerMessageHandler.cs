using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Scheduler.Events;

namespace Paramore.Brighter.Scheduler.Handlers;

/// <summary>
/// The fire scheduler message handler
/// </summary>
public class FireSchedulerMessageHandler(IAmACommandProcessor processor) : RequestHandlerAsync<FireSchedulerMessage>
{
    /// <inheritdoc />
    public override async Task<FireSchedulerMessage> HandleAsync(FireSchedulerMessage command, CancellationToken cancellationToken = default)
    {
        if (command.Async)
        {
            await processor.PostAsync(command, cancellationToken: cancellationToken);
        }
        else
        {
            processor.Post(command);
        }
        
        return await base.HandleAsync(command, cancellationToken);
    }
}
