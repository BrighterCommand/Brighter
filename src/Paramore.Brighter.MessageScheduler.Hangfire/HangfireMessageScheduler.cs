using System.Text.Json;
using Hangfire;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Scheduler.Events;

namespace Paramore.Brighter.MessageScheduler.Hangfire;

/// <summary>
/// The Hangfire message/request scheduler
/// </summary>
/// <param name="client">The <see cref="IBackgroundJobClientV2"/>.</param>
/// <param name="queue">The queue name.</param>
/// <param name="timeProvider">The <see cref="System.TimeProvider"/>.</param>
public class HangfireMessageScheduler(IBackgroundJobClientV2 client, string? queue, TimeProvider timeProvider)
    : IAmAMessageSchedulerSync, IAmAMessageSchedulerAsync, IAmARequestSchedulerSync, IAmARequestSchedulerAsync
{
    /// <inheritdoc />
    public string Schedule(Message message, DateTimeOffset at)
    {
        if (at < timeProvider.GetUtcNow())
        {
            throw new ArgumentOutOfRangeException(nameof(at), at, "Invalid at, it should be in the future");
        }

        return ScheduleMessage(message, false, at, null);
    }

    /// <inheritdoc />
    public string Schedule(Message message, TimeSpan delay)
    {
        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), delay, "Invalid delay, it can't be negative");
        }

        return ScheduleMessage(message, false, null, delay);
    }

    /// <inheritdoc />
    public string Schedule<TRequest>(TRequest request, RequestSchedulerType type, DateTimeOffset at)
        where TRequest : class, IRequest
    {
        if (at < timeProvider.GetUtcNow())
        {
            throw new ArgumentOutOfRangeException(nameof(at), at, "Invalid at, it should be in the future");
        }

        return ScheduleRequest(request, false, type, at, null);
    }

    /// <inheritdoc />
    public string Schedule<TRequest>(TRequest request, RequestSchedulerType type, TimeSpan delay)
        where TRequest : class, IRequest
    {
        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), delay, "Invalid delay, it can't be negative");
        }

        return ScheduleRequest(request, false, type, null, delay);
    }

    /// <inheritdoc cref="IAmAMessageSchedulerSync.ReScheduler(string,System.DateTimeOffset)" />
    public bool ReScheduler(string schedulerId, DateTimeOffset at)
    {
        if (at < timeProvider.GetUtcNow())
        {
            throw new ArgumentOutOfRangeException(nameof(at), at, "Invalid at, it should be in the future");
        }

        return client.Reschedule(schedulerId, at);
    }

    /// <inheritdoc cref="IAmAMessageSchedulerSync.ReScheduler(string,System.TimeSpan)" />
    public bool ReScheduler(string schedulerId, TimeSpan delay)
    {
        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), delay, "Invalid delay, it can't be negative");
        }

        return client.Reschedule(schedulerId, delay);
    }

    /// <inheritdoc cref="IAmAMessageSchedulerSync.Cancel" />
    public void Cancel(string id)
        => client.Delete(id);

    /// <inheritdoc />
    public Task<string> ScheduleAsync(Message message, DateTimeOffset at, CancellationToken cancellationToken = default)
    {
        if (at < timeProvider.GetUtcNow())
        {
            throw new ArgumentOutOfRangeException(nameof(at), at, "Invalid at, it should be in the future");
        }

        return Task.FromResult(ScheduleMessage(message, true, at, null));
    }

    /// <inheritdoc />
    public Task<string> ScheduleAsync(Message message, TimeSpan delay, CancellationToken cancellationToken = default)
    {
        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), delay, "Invalid delay, it can't be negative");
        }

        return Task.FromResult(ScheduleMessage(message, true, null, delay));
    }

    /// <inheritdoc />
    public Task<string> ScheduleAsync<TRequest>(TRequest request, RequestSchedulerType type, DateTimeOffset at,
        CancellationToken cancellationToken = default) where TRequest : class, IRequest
    {
        if (at < timeProvider.GetUtcNow())
        {
            throw new ArgumentOutOfRangeException(nameof(at), at, "Invalid at, it should be in the future");
        }

        return Task.FromResult(ScheduleRequest(request, true, type, at, null));
    }

    /// <inheritdoc />
    public Task<string> ScheduleAsync<TRequest>(TRequest request, RequestSchedulerType type, TimeSpan delay,
        CancellationToken cancellationToken = default) where TRequest : class, IRequest
    {
        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), delay, "Invalid delay, it can't be negative");
        }

        return Task.FromResult(ScheduleRequest(request, true, type, null, delay));
    }

    /// <inheritdoc cref="IAmAMessageSchedulerAsync.ReSchedulerAsync(string,System.DateTimeOffset,System.Threading.CancellationToken)"/>
    public Task<bool> ReSchedulerAsync(string schedulerId, DateTimeOffset at,
        CancellationToken cancellationToken = default)
        => Task.FromResult(ReScheduler(schedulerId, at));

    /// <inheritdoc cref="IAmAMessageSchedulerAsync.ReSchedulerAsync(string,System.TimeSpan,System.Threading.CancellationToken)" />
    public Task<bool> ReSchedulerAsync(string schedulerId, TimeSpan delay,
        CancellationToken cancellationToken = default)
        => Task.FromResult(ReScheduler(schedulerId, delay));

    /// <inheritdoc cref="IAmAMessageSchedulerAsync.CancelAsync" />
    public Task CancelAsync(string id, CancellationToken cancellationToken = default)
    {
        Cancel(id);
        return Task.CompletedTask;
    }

    private string ScheduleMessage(Message message, bool async, DateTimeOffset? at, TimeSpan? delay)
    {
        var scheduler = JsonSerializer.Serialize(new FireSchedulerMessage { Async = async, Message = message },
            JsonSerialisationOptions.Options);
        if (queue == null)
        {
            return at != null
                ? client.Schedule<BrighterHangfireSchedulerJob>(x => x.FireSchedulerMessageAsync(scheduler),
                    at.GetValueOrDefault())
                : client.Schedule<BrighterHangfireSchedulerJob>(x => x.FireSchedulerMessageAsync(scheduler),
                    delay.GetValueOrDefault());
        }

        return at != null
            ? client.Schedule<BrighterHangfireSchedulerJob>(queue, x => x.FireSchedulerMessageAsync(scheduler),
                at.GetValueOrDefault())
            : client.Schedule<BrighterHangfireSchedulerJob>(queue, x => x.FireSchedulerMessageAsync(scheduler),
                delay.GetValueOrDefault());
    }

    private string ScheduleRequest<TRequest>(TRequest request, bool async, RequestSchedulerType schedulerType,
        DateTimeOffset? at, TimeSpan? delay)
    {
        var scheduler = JsonSerializer.Serialize(
            new FireSchedulerRequest
            {
                Async = async,
                SchedulerType = schedulerType,
                RequestType = typeof(TRequest).FullName!,
                RequestData = JsonSerializer.Serialize(request, JsonSerialisationOptions.Options)
            },
            JsonSerialisationOptions.Options);
        if (queue == null)
        {
            return at != null
                ? client.Schedule<BrighterHangfireSchedulerJob>(x => x.FireSchedulerRequestAsync(scheduler),
                    at.GetValueOrDefault())
                : client.Schedule<BrighterHangfireSchedulerJob>(x => x.FireSchedulerRequestAsync(scheduler),
                    delay.GetValueOrDefault());
        }

        return at != null
            ? client.Schedule<BrighterHangfireSchedulerJob>(queue, x => x.FireSchedulerRequestAsync(scheduler),
                at.GetValueOrDefault())
            : client.Schedule<BrighterHangfireSchedulerJob>(queue, x => x.FireSchedulerRequestAsync(scheduler),
                delay.GetValueOrDefault());
    }
}
