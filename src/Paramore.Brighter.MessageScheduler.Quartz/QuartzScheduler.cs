using Paramore.Brighter.Scheduler.Events;
using Paramore.Brighter.Tasks;
using Quartz;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Paramore.Brighter.MessageScheduler.Quartz;

/// <summary>
/// The Quartz Message scheduler
/// </summary>
/// <param name="scheduler"></param>
/// <param name="group"></param>
/// <param name="getOrCreateMessageSchedulerId"></param>
public class QuartzScheduler(
    IScheduler scheduler,
    string? group,
    TimeProvider timeProvider,
    Func<Message, string> getOrCreateMessageSchedulerId,
    Func<IRequest, string> getOrCreateRequestSchedulerId)
    : IAmAMessageSchedulerSync, IAmAMessageSchedulerAsync, IAmARequestSchedulerSync, IAmARequestSchedulerAsync
{
    /// <inheritdoc />
    public string Schedule(Message message, DateTimeOffset at)
    {
        if (at < timeProvider.GetUtcNow())
        {
            throw new ArgumentOutOfRangeException(nameof(at), at, "Invalid at, it should be in the future");
        }

        var id = getOrCreateMessageSchedulerId(message);
        var job = JobBuilder.Create<QuartzBrighterJob>()
            .WithIdentity(id, group!)
            .UsingJobData("message", JsonSerializer.Serialize(
                new FireSchedulerMessage { Id = id, Async = false, Message = message },
                JsonSerialisationOptions.Options))
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity(id + "-trigger", group!)
            .StartAt(at)
            .Build();

        BrighterAsyncContext.Run(async () => await scheduler.ScheduleJob(job, trigger));
        return id;
    }

    /// <inheritdoc />
    public string Schedule(Message message, TimeSpan delay)
    {
        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), delay, "Invalid delay, it can't be negative");
        }

        return Schedule(message, timeProvider.GetUtcNow().Add(delay));
    }

    /// <inheritdoc />
    public string Schedule<TRequest>(TRequest request, RequestSchedulerType type, DateTimeOffset at)
        where TRequest : class, IRequest
    {
        if (at < timeProvider.GetUtcNow())
        {
            throw new ArgumentOutOfRangeException(nameof(at), "Invalid at, it should be in the future");
        }

        var id = getOrCreateRequestSchedulerId(request);
        var job = JobBuilder.Create<QuartzBrighterJob>()
            .WithIdentity(id, group!)
            .UsingJobData("request", JsonSerializer.Serialize(
                new FireSchedulerRequest
                {
                    Id = id,
                    Async = false,
                    SchedulerType = type,
                    RequestType = typeof(TRequest).FullName!,
                    RequestData = JsonSerializer.Serialize(request, JsonSerialisationOptions.Options)
                },
                JsonSerialisationOptions.Options))
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity(id + "-trigger", group!)
            .StartAt(at)
            .Build();

        BrighterAsyncContext.Run(async () => await scheduler.ScheduleJob(job, trigger));
        return id;
    }

    /// <inheritdoc />
    public string Schedule<TRequest>(TRequest request, RequestSchedulerType type, TimeSpan delay)
        where TRequest : class, IRequest
    {
        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), delay, "Invalid delay, it can't be negative");
        }

        return Schedule(request, type, timeProvider.GetUtcNow().Add(delay));
    }

    /// <inheritdoc cref="IAmAMessageSchedulerSync.ReScheduler(string,System.DateTimeOffset)"/>
    public bool ReScheduler(string schedulerId, DateTimeOffset at)
        => BrighterAsyncContext.Run(async () => await ReSchedulerAsync(schedulerId, at));

    /// <inheritdoc cref="IAmAMessageSchedulerSync.ReScheduler(string,System.TimeSpan)" />
    public bool ReScheduler(string schedulerId, TimeSpan delay)
    {
        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), "Invalid delay, it can't be negative");
        }

        return ReScheduler(schedulerId, timeProvider.GetUtcNow().Add(delay));
    }

    /// <inheritdoc cref="IAmAMessageSchedulerSync.Cancel"/>
    public void Cancel(string id)
        => BrighterAsyncContext.Run(async () => await CancelAsync(id));

    /// <inheritdoc />
    public async Task<string> ScheduleAsync(Message message, DateTimeOffset at,
        CancellationToken cancellationToken = default)
    {
        if (at < timeProvider.GetUtcNow())
        {
            throw new ArgumentOutOfRangeException(nameof(at), "Invalid at, it should be in the future");
        }

        var id = getOrCreateMessageSchedulerId(message);
        var job = JobBuilder.Create<QuartzBrighterJob>()
            .WithIdentity(id, group!)
            .UsingJobData("message", JsonSerializer.Serialize(
                new FireSchedulerMessage { Id = id, Async = true, Message = message },
                JsonSerialisationOptions.Options))
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity(id + "-trigger", group!)
            .StartAt(at)
            .Build();

        await scheduler.ScheduleJob(job, trigger, cancellationToken);
        return id;
    }

    /// <inheritdoc />
    public async Task<string> ScheduleAsync(Message message, TimeSpan delay,
        CancellationToken cancellationToken = default)
    {
        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), delay, "Invalid delay, it can't be negative");
        }

        return await ScheduleAsync(message, timeProvider.GetUtcNow().Add(delay), cancellationToken);
    }

    public async Task<string> ScheduleAsync<TRequest>(TRequest request, RequestSchedulerType type, DateTimeOffset at,
        CancellationToken cancellationToken = default) where TRequest : class, IRequest
    {
        if (at < timeProvider.GetUtcNow())
        {
            throw new ArgumentOutOfRangeException(nameof(at), at, "Invalid at, it should be in the future");
        }

        var id = getOrCreateRequestSchedulerId(request);
        var job = JobBuilder.Create<QuartzBrighterJob>()
            .WithIdentity(id, group!)
            .UsingJobData("request", JsonSerializer.Serialize(
                new FireSchedulerRequest
                {
                    Id = id,
                    Async = true,
                    SchedulerType = type,
                    RequestType = typeof(TRequest).FullName!,
                    RequestData = JsonSerializer.Serialize(request, JsonSerialisationOptions.Options)
                },
                JsonSerialisationOptions.Options))
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity(id + "-trigger", group!)
            .StartAt(at)
            .Build();

        await scheduler.ScheduleJob(job, trigger, cancellationToken);
        return id;
    }

    /// <inheritdoc />
    public async Task<string> ScheduleAsync<TRequest>(TRequest request, RequestSchedulerType type, TimeSpan delay,
        CancellationToken cancellationToken = default) where TRequest : class, IRequest
    {
        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), delay, "Invalid delay, it can't be negative");
        }

        return await ScheduleAsync(request, type, timeProvider.GetUtcNow().Add(delay), cancellationToken);
    }

    /// <inheritdoc cref="IAmAMessageSchedulerAsync.ReSchedulerAsync(string,System.DateTimeOffset,System.Threading.CancellationToken)"/>
    public async Task<bool> ReSchedulerAsync(string schedulerId, DateTimeOffset at,
        CancellationToken cancellationToken = default)
    {
        if (at < timeProvider.GetUtcNow())
        {
            throw new ArgumentOutOfRangeException(nameof(at), at, "Invalid at, it should be in the future");
        }

        var date = await scheduler.RescheduleJob(new TriggerKey(schedulerId + "-trigger", group!), TriggerBuilder
            .Create()
            .WithIdentity(schedulerId + "-trigger", group!)
            .StartAt(at)
            .Build(), cancellationToken);

        return date != null;
    }

    /// <inheritdoc cref="IAmAMessageSchedulerAsync.ReSchedulerAsync(string,System.TimeSpan,System.Threading.CancellationToken)"/>
    public async Task<bool> ReSchedulerAsync(string schedulerId, TimeSpan delay,
        CancellationToken cancellationToken = default)
    {
        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), delay, "Invalid delay, it can't be negative");
        }

        return await ReSchedulerAsync(schedulerId, timeProvider.GetUtcNow().Add(delay), cancellationToken);
    }

    /// <inheritdoc cref="IAmAMessageSchedulerAsync.CancelAsync"/>
    public async Task CancelAsync(string id, CancellationToken cancellationToken = default)
        => await scheduler.DeleteJob(new JobKey(id, group!), cancellationToken);
}
