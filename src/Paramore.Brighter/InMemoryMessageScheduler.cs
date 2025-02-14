using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Scheduler.Events;
using Paramore.Brighter.Tasks;
using InvalidOperationException = System.InvalidOperationException;

namespace Paramore.Brighter;

public class InMemoryMessageScheduler(
    IAmACommandProcessor processor,
    TimeProvider timeProvider,
    Func<Message, string> getOrCreateSchedulerId,
    OnSchedulerConflict onConflict)
    : IAmAMessageSchedulerSync, IAmAMessageSchedulerAsync
{
    private static readonly ConcurrentDictionary<string, ITimer> s_timers = new();
    private static readonly ILogger Logger = ApplicationLogging.CreateLogger<InMemoryMessageScheduler>();

    /// <inheritdoc cref="Schedule(Paramore.Brighter.Message,System.DateTimeOffset)"/>
    public string Schedule(Message message, DateTimeOffset at)
        => Schedule(message, at - DateTimeOffset.UtcNow);

    /// <inheritdoc cref="Schedule(Paramore.Brighter.Message,System.TimeSpan)"/>
    public string Schedule(Message message, TimeSpan delay)
    {
        var id = getOrCreateSchedulerId(message);
        if (s_timers.TryGetValue(id, out var timer))
        {
            if (onConflict == OnSchedulerConflict.Throw)
            {
                throw new InvalidOperationException($"scheduler with '{id}' id already exists");
            }

            timer.Dispose();
        }

        s_timers[id] = timeProvider.CreateTimer(Execute, (processor, id, message, false), delay, TimeSpan.Zero);
        return id;
    }

    /// <inheritdoc cref="ReScheduler(System.String,System.DateTimeOffset)"/>
    public bool ReScheduler(string schedulerId, DateTimeOffset at)
        => ReScheduler(schedulerId, at - timeProvider.GetUtcNow());

    /// <inheritdoc cref="ReScheduler(System.String,System.TimeSpan)"/>
    public bool ReScheduler(string schedulerId, TimeSpan delay)
    {
        if (s_timers.TryGetValue(schedulerId, out var timer))
        {
            timer.Change(delay, TimeSpan.Zero);
            return true;
        }

        return false;
    }

    /// <inheritdoc cref="Cancel"/>
    public void Cancel(string id)
    {
        if (s_timers.TryRemove(id, out var timer))
        {
            timer.Dispose();
        }
    }

    /// <inheritdoc cref="ScheduleAsync(Paramore.Brighter.Message,System.DateTimeOffset,System.Threading.CancellationToken)"/>
    public async Task<string> ScheduleAsync(Message message, DateTimeOffset at,
        CancellationToken cancellationToken = default)
        => await ScheduleAsync(message, at - timeProvider.GetUtcNow(), cancellationToken);

    /// <inheritdoc cref="ScheduleAsync(Paramore.Brighter.Message,System.TimeSpan,System.Threading.CancellationToken)"/>
    public async Task<string> ScheduleAsync(Message message, TimeSpan delay,
        CancellationToken cancellationToken = default)
    {
        var id = getOrCreateSchedulerId(message);
        if (s_timers.TryGetValue(id, out var timer))
        {
            if (onConflict == OnSchedulerConflict.Throw)
            {
                throw new InvalidOperationException($"scheduler with '{id}' id already exists");
            }

            await timer.DisposeAsync();
        }

        s_timers[id] = timeProvider.CreateTimer(Execute, (processor, id, message, true), delay, TimeSpan.Zero);
        return id;
    }

    /// <inheritdoc cref="ReSchedulerAsync(string,System.DateTimeOffset,System.Threading.CancellationToken)"/>
    public Task<bool> ReSchedulerAsync(string schedulerId, DateTimeOffset at,
        CancellationToken cancellationToken = default)
        => Task.FromResult(ReScheduler(schedulerId, at));

    /// <inheritdoc cref="ReSchedulerAsync(string,System.TimeSpan,System.Threading.CancellationToken)"/>
    public Task<bool> ReSchedulerAsync(string schedulerId, TimeSpan delay,
        CancellationToken cancellationToken = default)
        => Task.FromResult(ReScheduler(schedulerId, delay));

    /// <inheritdoc cref="CancelAsync"/> 
    public async Task CancelAsync(string id, CancellationToken cancellationToken = default)
    {
        if (s_timers.TryRemove(id, out var timer))
        {
            await timer.DisposeAsync();
        }
    }

    /// <inheritdoc cref="Dispose"/>
    public void Dispose()
    {
    }

    /// <inheritdoc cref="DisposeAsync"/>
    public ValueTask DisposeAsync()
    {
        return new ValueTask();
    }

    private static void Execute(object? state)
    {
        var (processor, id, message, async) = ((IAmACommandProcessor, string, Message, bool))state!;
        try
        {
            BrighterAsyncContext.Run(async () => await processor.SendAsync(new FireSchedulerMessage { Id = id, Message = message }));
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Error during processing scheduler {Id}", id);
        }

        if (s_timers.TryRemove(id, out var timer))
        {
            timer.Dispose();
        }
    }
}
