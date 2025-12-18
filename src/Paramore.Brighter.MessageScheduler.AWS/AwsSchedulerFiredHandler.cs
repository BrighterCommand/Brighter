using Paramore.Brighter.Scheduler.Events;

namespace Paramore.Brighter.MessageScheduler.AWS;

/// <summary>
/// The <see cref="FireAwsScheduler"/> handler
/// </summary>
/// <param name="processor"></param>
public class AwsSchedulerFiredHandler(IAmACommandProcessor processor) : RequestHandlerAsync<FireAwsScheduler>
{
    /// <inheritdoc />
    public override async Task<FireAwsScheduler> HandleAsync(
        FireAwsScheduler command,
        CancellationToken cancellationToken = default)
    {
        if (command.Message is not null)
        {
            await processor.SendAsync(
                new FireSchedulerMessage { Id = command.Id, Async = command.Async, Message = command.Message },
                cancellationToken: cancellationToken);
        }
        else if (!string.IsNullOrEmpty(command.RequestType) && !string.IsNullOrEmpty(command.RequestData))
        {
            await processor.SendAsync(
                new FireSchedulerRequest
                {
                    Id = command.Id,
                    Async = command.Async,
                    SchedulerType = command.SchedulerType,
                    RequestType = command.RequestType!,
                    RequestData = command.RequestData!
                }, cancellationToken: cancellationToken);
        }
        else
        {
            throw new InvalidOperationException("Error during handling the scheduler message, not request or message was set");
        }

        return await base.HandleAsync(command, cancellationToken);
    }
}
