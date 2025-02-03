using Hangfire;
using Paramore.Brighter.Scheduler.Events;

namespace Paramore.Brighter.MessageScheduler.Hangfire;

/// <summary>
/// The Hangfire adaptor for <see cref="IAmAMessageScheduler"/>.
/// </summary>
/// <param name="client"></param>
/// <param name="queue"></param>
public class HangfireMessageScheduler(
    IAmACommandProcessor processor,
    IBackgroundJobClientV2 client,
    string? queue) : IAmAMessageSchedulerSync, IAmAMessageSchedulerAsync
{
    /// <inheritdoc cref="Schedule(Paramore.Brighter.Message,System.DateTimeOffset)"/>
    public string Schedule(Message message, DateTimeOffset at)
        => client.Schedule(queue, () => ConsumeAsync(message), at);

    /// <inheritdoc cref="Schedule(Paramore.Brighter.Message,System.TimeSpan)"/>
    public string Schedule(Message message, TimeSpan delay)
        => client.Schedule(queue, () => ConsumeAsync(message), delay);

    /// <inheritdoc cref="ReScheduler(System.String,System.DateTimeOffset)"/>
    public bool ReScheduler(string schedulerId, DateTimeOffset at) => client.Reschedule(schedulerId, at);

    /// <inheritdoc cref="ReScheduler(System.String,System.TimeSpan)"/>
    public bool ReScheduler(string schedulerId, TimeSpan delay) => client.Reschedule(schedulerId, delay);

    /// <inheritdoc cref="Cancel"/>
    public void Cancel(string id) => client.Delete(queue, id);

    private async Task ConsumeAsync(Message message) 
        => await processor.SendAsync(new SchedulerMessageFired { Id = message.Id, Message = message });

    /// <inheritdoc cref="ScheduleAsync(Paramore.Brighter.Message,System.DateTimeOffset,System.Threading.CancellationToken)"/>
    public Task<string> ScheduleAsync(Message message, DateTimeOffset at, CancellationToken cancellationToken = default)
        => Task.FromResult(Schedule(message, at));

    /// <inheritdoc cref="ScheduleAsync(Paramore.Brighter.Message,System.TimeSpan,System.Threading.CancellationToken)"/>
    public Task<string> ScheduleAsync(Message message, TimeSpan delay, CancellationToken cancellationToken = default)
        => Task.FromResult(Schedule(message, delay));

    /// <inheritdoc cref="ReSchedulerAsync(string,System.DateTimeOffset,System.Threading.CancellationToken)"/>
    public Task<bool> ReSchedulerAsync(string schedulerId, DateTimeOffset at,
        CancellationToken cancellationToken = default)
        => Task.FromResult(ReScheduler(schedulerId, at));

    /// <inheritdoc cref="ReSchedulerAsync(string,System.TimeSpan,System.Threading.CancellationToken)"/>
    public Task<bool> ReSchedulerAsync(string schedulerId, TimeSpan delay,
        CancellationToken cancellationToken = default)
        => Task.FromResult(ReScheduler(schedulerId, delay));

    /// <inheritdoc cref="CancelAsync"/> 
    public Task CancelAsync(string id, CancellationToken cancellationToken = default)
    {
        Cancel(id);
        return Task.CompletedTask;
    }

    /// <inheritdoc cref="DisposeAsync"/>
    public ValueTask DisposeAsync() => new();

    /// <inheritdoc cref="Dispose"/>
    public void Dispose()
    {
    }
}
