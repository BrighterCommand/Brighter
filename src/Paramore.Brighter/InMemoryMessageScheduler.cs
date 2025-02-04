using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Scheduler.Events;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter;

public class InMemoryMessageScheduler(IAmACommandProcessor processor,
    TimeProvider timeProvider,
    Func<Message, string> getOrCreateSchedulerId,
    OnSchedulerConflict onConflict)
    : IAmAMessageSchedulerSync, IAmAMessageSchedulerAsync
{
    private readonly ConcurrentDictionary<string, ITimer> _timers = new();

    /// <inheritdoc cref="Schedule(Paramore.Brighter.Message,System.DateTimeOffset)"/>
    public string Schedule(Message message, DateTimeOffset at) 
        => Schedule(message, at - DateTimeOffset.UtcNow);

    /// <inheritdoc cref="Schedule(Paramore.Brighter.Message,System.TimeSpan)"/>
    public string Schedule(Message message, TimeSpan delay)
    {
        var id = getOrCreateSchedulerId(message);
        if (_timers.TryGetValue(id, out var timer))
        {
            if (onConflict == OnSchedulerConflict.Throw)
            {
                throw new InvalidOperationException($"scheduler with '{id}' id already exists");
            }
            
            timer.Dispose();
        }
        
        _timers[id] = timeProvider.CreateTimer(_ => Execute(id, message), null, delay, TimeSpan.Zero);
        return id;
    }

    /// <inheritdoc cref="ReScheduler(System.String,System.DateTimeOffset)"/>
    public bool ReScheduler(string schedulerId, DateTimeOffset at)
        => ReScheduler(schedulerId, at - DateTimeOffset.UtcNow); 

    /// <inheritdoc cref="ReScheduler(System.String,System.TimeSpan)"/>
    public bool ReScheduler(string schedulerId, TimeSpan delay)
    {
        if(_timers.TryGetValue(schedulerId, out var timer))
        {
            timer.Change(delay, TimeSpan.Zero);
            return true;
        }
        
        return false;
    }

    /// <inheritdoc cref="Cancel"/>
    public void Cancel(string id)
    {
        if (_timers.TryRemove(id, out var timer))
        {
            timer.Dispose();
        }
    }

    /// <inheritdoc cref="ScheduleAsync(Paramore.Brighter.Message,System.DateTimeOffset,System.Threading.CancellationToken)"/>
    public Task<string> ScheduleAsync(Message message, DateTimeOffset at, CancellationToken cancellationToken = default)
        => Task.FromResult(Schedule(message, at));

    /// <inheritdoc cref="ScheduleAsync(Paramore.Brighter.Message,System.TimeSpan,System.Threading.CancellationToken)"/>
    public async Task<string> ScheduleAsync(Message message, TimeSpan delay, CancellationToken cancellationToken = default)
    {
        var id = getOrCreateSchedulerId(message);
        if (_timers.TryGetValue(id, out var timer))
        {
            if (onConflict == OnSchedulerConflict.Throw)
            {
                throw new InvalidOperationException($"scheduler with '{id}' id already exists");
            }
            
            await timer.DisposeAsync();
        }
        
        _timers[id] = timeProvider.CreateTimer(_ => Execute(id, message), null, delay, TimeSpan.Zero);
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
        if (_timers.TryRemove(id, out var timer))
        {
            await timer.DisposeAsync();
        }
    }

    /// <inheritdoc cref="Dispose"/>
    public void Dispose()
    {
        foreach (var timer in _timers.Values)
        {
            timer.Dispose();
        }
        
        _timers.Clear();
    }

    /// <inheritdoc cref="DisposeAsync"/>
    public async ValueTask DisposeAsync()
    {
        foreach (var timer in _timers.Values)
        {
            await timer.DisposeAsync();
        }

        _timers.Clear();
    }

    private void Execute(string id, Message message)
    {
        BrighterAsyncContext.Run(async () => await processor.PostAsync(new FireSchedulerMessage
        {
            Id = id,
            Message = message
        }));

        if (_timers.TryRemove(id, out var timer))
        {
            timer.Dispose();
        }
    }
}
