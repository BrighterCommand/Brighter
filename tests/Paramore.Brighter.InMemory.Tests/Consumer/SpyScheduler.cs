using System;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.InMemory.Tests.Consumer;

internal sealed class SpyScheduler : IAmAMessageSchedulerSync, IAmAMessageSchedulerAsync
{
    public bool ScheduleCalled { get; private set; }
    public Message? ScheduledMessage { get; private set; }
    public TimeSpan? ScheduledDelay { get; private set; }

    public string Schedule(Message message, DateTimeOffset at)
    {
        ScheduleCalled = true;
        ScheduledMessage = message;
        return Guid.NewGuid().ToString();
    }

    public string Schedule(Message message, TimeSpan delay)
    {
        ScheduleCalled = true;
        ScheduledMessage = message;
        ScheduledDelay = delay;
        return Guid.NewGuid().ToString();
    }

    public bool ReScheduler(string schedulerId, DateTimeOffset at) => true;

    public bool ReScheduler(string schedulerId, TimeSpan delay) => true;

    public void Cancel(string id) { }

    public Task<string> ScheduleAsync(Message message, DateTimeOffset at, CancellationToken cancellationToken = default)
        => Task.FromResult(Schedule(message, at));

    public Task<string> ScheduleAsync(Message message, TimeSpan delay, CancellationToken cancellationToken = default)
        => Task.FromResult(Schedule(message, delay));

    public Task<bool> ReSchedulerAsync(string schedulerId, DateTimeOffset at, CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    public Task<bool> ReSchedulerAsync(string schedulerId, TimeSpan delay, CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    public Task CancelAsync(string id, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
