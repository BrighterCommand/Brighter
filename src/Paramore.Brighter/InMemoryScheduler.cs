#region Licence
/* The MIT License (MIT)
Copyright © 2025 Rafael Andrade
    

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Scheduler.Events;
using Paramore.Brighter.Tasks;
using InvalidOperationException = System.InvalidOperationException;

namespace Paramore.Brighter;

/// <summary>
/// The In-Memory scheduler
/// </summary>
/// <param name="processor">The <see cref="IAmACommandProcessor"/>.</param>
/// <param name="timeProvider">The <see cref="TimeProvider"/>.</param>
/// <param name="getOrCreateRequestSchedulerId">The get or create request scheduler id</param>
/// <param name="getOrCreateMessageSchedulerId">The get or create message scheduler id</param>
/// <param name="onConflict">Action performance on conflict</param>
public class InMemoryScheduler(
    IAmACommandProcessor processor,
    TimeProvider timeProvider,
    Func<IRequest, string> getOrCreateRequestSchedulerId,
    Func<Message, string> getOrCreateMessageSchedulerId,
    OnSchedulerConflict onConflict)
    : IAmAMessageSchedulerSync, IAmAMessageSchedulerAsync, IAmARequestSchedulerSync, IAmARequestSchedulerAsync, IDisposable, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, ITimer> _timers = new();
    private static readonly ILogger Logger = ApplicationLogging.CreateLogger<InMemoryScheduler>();

    /// <inheritdoc />
    public string Schedule(Message message, DateTimeOffset at)
        => Schedule(message, at - timeProvider.GetUtcNow());

    /// <inheritdoc />
    public string Schedule(Message message, TimeSpan delay)
    {
        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), delay, "Invalid delay, it can't be negative");
        }

        var id = getOrCreateMessageSchedulerId(message);
        var state = (processor, new FireSchedulerMessage { Id = id, Async = false, Message = message });
        return ScheduleTimer(id, state, delay);
    }

    /// <inheritdoc />
    public string Schedule<TRequest>(TRequest request, RequestSchedulerType type, DateTimeOffset at)
        where TRequest : class, IRequest
    {
        if (at < timeProvider.GetUtcNow())
        {
            throw new ArgumentOutOfRangeException(nameof(at), at, "invalid datetime, it should be in the future");
        }

        return Schedule(request, type, at - timeProvider.GetUtcNow());
    }

    /// <inheritdoc />
    public string Schedule<TRequest>(TRequest request, RequestSchedulerType type, TimeSpan delay)
        where TRequest : class, IRequest
    {
        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), delay, "Invalid delay, it can't be negative");
        }

        var id = getOrCreateRequestSchedulerId(request);
        var state = (processor,
            new FireSchedulerRequest
            {
                Id = id,
                Async = false,
                SchedulerType = type,
                RequestType = typeof(TRequest).FullName!,
                RequestData = JsonSerializer.Serialize(request, JsonSerialisationOptions.Options)
            });
        return ScheduleTimer(id, state, delay);
    }

    /// <inheritdoc cref="IAmAMessageSchedulerSync.ReScheduler(string,System.DateTimeOffset)"/>
    public bool ReScheduler(string schedulerId, DateTimeOffset at)
    {
        if (at < timeProvider.GetUtcNow())
        {
            throw new ArgumentOutOfRangeException(nameof(at), at, "Invalid at, it should be in the future");
        }

        return ReScheduler(schedulerId, at - timeProvider.GetUtcNow());
    }

    /// <inheritdoc cref="IAmAMessageSchedulerSync.ReScheduler(string,System.TimeSpan)"/>
    public bool ReScheduler(string schedulerId, TimeSpan delay)
    {
        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), delay, "Invalid delay, it can't be negative");
        }

        if (_timers.TryGetValue(schedulerId, out var timer))
        {
            timer.Change(delay, TimeSpan.Zero);
            return true;
        }

        return false;
    }

    /// <inheritdoc cref="IAmAMessageSchedulerSync.Cancel" />
    public void Cancel(string id)
    {
        if (_timers.TryRemove(id, out var timer))
        {
            timer.Dispose();
        }
    }

    /// <inheritdoc />
    public Task<string> ScheduleAsync(Message message, DateTimeOffset at,
        CancellationToken cancellationToken = default)
    {
        if (at < timeProvider.GetUtcNow())
        {
            throw new ArgumentOutOfRangeException(nameof(at), at, "Invalid at, it should be in the future");
        }

        return ScheduleAsync(message, at - timeProvider.GetUtcNow(), cancellationToken);
    }

    /// <inheritdoc />
    public Task<string> ScheduleAsync(Message message, TimeSpan delay,
        CancellationToken cancellationToken = default)
    {
        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), delay, "Invalid delay, it can't be negative");
        }

        var id = getOrCreateMessageSchedulerId(message);
        var state = (processor, new FireSchedulerMessage { Id = id, Async = true, Message = message });
        return Task.FromResult(ScheduleTimer(id, state, delay));
    }

    /// <inheritdoc />
    public Task<string> ScheduleAsync<TRequest>(TRequest request, RequestSchedulerType type, DateTimeOffset at,
        CancellationToken cancellationToken = default)
        where TRequest : class, IRequest
    {
        if (at < timeProvider.GetUtcNow())
        {
            throw new ArgumentOutOfRangeException(nameof(at), at, "Invalid at, it should be in the future");
        }

        return ScheduleAsync(request, type, at - timeProvider.GetUtcNow(), cancellationToken);
    }

    /// <inheritdoc />
    public Task<string> ScheduleAsync<TRequest>(TRequest request, RequestSchedulerType type, TimeSpan delay,
        CancellationToken cancellationToken = default) where TRequest : class, IRequest
    {
        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), delay, "Invalid delay, it can't be negative");
        }

        var id = getOrCreateRequestSchedulerId(request);
        var state = (processor,
            new FireSchedulerRequest
            {
                Id = id,
                Async = true,
                SchedulerType = type,
                RequestType = typeof(TRequest).FullName!,
                RequestData = JsonSerializer.Serialize(request, JsonSerialisationOptions.Options)
            });
        return Task.FromResult(ScheduleTimer(id, state, delay));
    }

    /// <inheritdoc cref="IAmAMessageSchedulerAsync.ReSchedulerAsync(string,System.DateTimeOffset,System.Threading.CancellationToken)" />
    public Task<bool> ReSchedulerAsync(string schedulerId, DateTimeOffset at,
        CancellationToken cancellationToken = default)
        => Task.FromResult(ReScheduler(schedulerId, at));

    /// <inheritdoc cref="IAmAMessageSchedulerAsync.ReSchedulerAsync(string,System.TimeSpan,System.Threading.CancellationToken)"/>
    public Task<bool> ReSchedulerAsync(string schedulerId, TimeSpan delay,
        CancellationToken cancellationToken = default)
        => Task.FromResult(ReScheduler(schedulerId, delay));

    /// <inheritdoc cref="IAmAMessageSchedulerAsync.CancelAsync" /> 
    public async Task CancelAsync(string id, CancellationToken cancellationToken = default)
    {
        if (_timers.TryRemove(id, out var timer))
        {
            await timer.DisposeAsync();
        }
    }

    private string ScheduleTimer(string id, object state, TimeSpan delay)
    {
        if (onConflict == OnSchedulerConflict.Throw)
        {
            // We create the timer before TryAdd to avoid a TOCTOU race between ContainsKey and AddOrUpdate.
            // If TryAdd fails (key already exists), we pay the cost of disposing the unused timer — an
            // acceptable trade-off to guarantee that a concurrent duplicate is never silently overwritten.
            var timer = timeProvider.CreateTimer(Execute, state, delay, TimeSpan.Zero);
            if (!_timers.TryAdd(id, timer))
            {
                timer.Dispose();
                throw new InvalidOperationException($"scheduler with '{id}' id already exists");
            }
        }
        else
        {
            _timers.AddOrUpdate(
                id,
                _ => timeProvider.CreateTimer(Execute, state, delay, TimeSpan.Zero),
                (_, existingTimer) =>
                {
                    existingTimer.Dispose();
                    return timeProvider.CreateTimer(Execute, state, delay, TimeSpan.Zero);
                });
        }

        return id;
    }

    private void Execute(object? state)
    {
        // .NET Standard doesn't support if(state is (IAmACommandProcessor, FireSchedulerMessage))
        var fireMessage = state as (IAmACommandProcessor, FireSchedulerMessage)?;
        if (fireMessage != null)
        {
            var (processor, message) = (fireMessage.Value.Item1, fireMessage.Value.Item2);
            BrighterAsyncContext.Run(() => processor.SendAsync(message));

            if (_timers.TryRemove(message.Id, out var timer))
            {
                timer.Dispose();
            }

            return;
        }

        var fireRequest = state as (IAmACommandProcessor, FireSchedulerRequest)?;
        if (fireRequest != null)
        {
            var (processor, request) = (fireRequest.Value.Item1, fireRequest.Value.Item2);
            BrighterAsyncContext.Run(() => processor.SendAsync(request));

            if (_timers.TryRemove(request.Id, out var timer))
            {
                timer.Dispose();
            }

            return;
        }

        Logger.LogError("Invalid input during executing scheduler {Data}", state);
    }

    /// <summary>
    /// Disposes all active timers held by this scheduler.
    /// </summary>
    public void Dispose()
    {
        foreach (var kvp in _timers)
            kvp.Value.Dispose();
        _timers.Clear();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Asynchronously disposes all active timers held by this scheduler.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        foreach (var kvp in _timers)
            await kvp.Value.DisposeAsync();
        _timers.Clear();
        GC.SuppressFinalize(this);
    }
}
