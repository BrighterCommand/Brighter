using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Scheduler;
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
    : IAmAMessageSchedulerSync, IAmAMessageSchedulerAsync, IAmARequestSchedulerSyncWithContext, IAmARequestSchedulerAsyncWithContext
{
    /// <inheritdoc />
    public string Schedule(Message message, DateTimeOffset at)
    {
        if (at < timeProvider.GetUtcNow())
        {
            throw new ArgumentOutOfRangeException(nameof(at), at, "Invalid at, it should be in the future");
        }

        return ScheduleCore(message, at);
    }

    private string ScheduleCore(Message message, DateTimeOffset at)
    {
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

        BrighterAsyncContext.Run(() => scheduler.ScheduleJob(job, trigger));
        return id;
    }

    /// <inheritdoc />
    public string Schedule(Message message, TimeSpan delay)
    {
        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), delay, "Invalid delay, it can't be negative");
        }

        return ScheduleCore(message, timeProvider.GetUtcNow().Add(delay));
    }

    /// <inheritdoc />
    public string Schedule<TRequest>(TRequest request, RequestSchedulerType type, DateTimeOffset at)
        where TRequest : class, IRequest
        => Schedule(request, type, at, null);

    /// <inheritdoc />
    public string Schedule<TRequest>(TRequest request, RequestSchedulerType type, DateTimeOffset at,
        IRequestContext? requestContext)
        where TRequest : class, IRequest
    {
        if (at < timeProvider.GetUtcNow())
        {
            throw new ArgumentOutOfRangeException(nameof(at), "Invalid at, it should be in the future");
        }

        return ScheduleCore(request, type, at, requestContext);
    }

    private string ScheduleCore<TRequest>(TRequest request, RequestSchedulerType type, DateTimeOffset at,
        IRequestContext? requestContext)
        where TRequest : class, IRequest
    {
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
                    RequestData = JsonSerializer.Serialize(request, JsonSerialisationOptions.Options),
                    RequestContextData = ScheduledRequestContext.Serialize(requestContext)
                },
                JsonSerialisationOptions.Options))
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity(id + "-trigger", group!)
            .StartAt(at)
            .Build();

        BrighterAsyncContext.Run(() => scheduler.ScheduleJob(job, trigger));
        return id;
    }

    /// <inheritdoc />
    public string Schedule<TRequest>(TRequest request, RequestSchedulerType type, TimeSpan delay)
        where TRequest : class, IRequest
        => Schedule(request, type, delay, null);

    /// <inheritdoc />
    public string Schedule<TRequest>(TRequest request, RequestSchedulerType type, TimeSpan delay,
        IRequestContext? requestContext)
        where TRequest : class, IRequest
    {
        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), delay, "Invalid delay, it can't be negative");
        }

        return ScheduleCore(request, type, timeProvider.GetUtcNow().Add(delay), requestContext);
    }

    /// <inheritdoc cref="IAmAMessageSchedulerSync.ReScheduler(string,System.DateTimeOffset)"/>
    public bool ReScheduler(string schedulerId, DateTimeOffset at)
        => BrighterAsyncContext.Run(() => ReSchedulerAsync(schedulerId, at));

    /// <inheritdoc cref="IAmAMessageSchedulerSync.ReScheduler(string,System.TimeSpan)" />
    public bool ReScheduler(string schedulerId, TimeSpan delay)
    {
        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), "Invalid delay, it can't be negative");
        }

        var at = timeProvider.GetUtcNow().Add(delay);
        return BrighterAsyncContext.Run(() => ReSchedulerCoreAsync(schedulerId, at, CancellationToken.None));
    }

    /// <inheritdoc cref="IAmAMessageSchedulerSync.Cancel"/>
    public void Cancel(string id)
        => BrighterAsyncContext.Run(() => CancelAsync(id));

    /// <inheritdoc />
    public async Task<string> ScheduleAsync(Message message, DateTimeOffset at,
        CancellationToken cancellationToken = default)
    {
        if (at < timeProvider.GetUtcNow())
        {
            throw new ArgumentOutOfRangeException(nameof(at), "Invalid at, it should be in the future");
        }

        return await ScheduleCoreAsync(message, at, cancellationToken);
    }

    private async Task<string> ScheduleCoreAsync(Message message, DateTimeOffset at,
        CancellationToken cancellationToken)
    {
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

        return await ScheduleCoreAsync(message, timeProvider.GetUtcNow().Add(delay), cancellationToken);
    }

    /// <inheritdoc />
    public Task<string> ScheduleAsync<TRequest>(TRequest request, RequestSchedulerType type, DateTimeOffset at,
        CancellationToken cancellationToken = default) where TRequest : class, IRequest
        => ScheduleAsync(request, type, at, null, cancellationToken);

    /// <inheritdoc />
    public async Task<string> ScheduleAsync<TRequest>(TRequest request, RequestSchedulerType type, DateTimeOffset at,
        IRequestContext? requestContext, CancellationToken cancellationToken)
        where TRequest : class, IRequest
    {
        if (at < timeProvider.GetUtcNow())
        {
            throw new ArgumentOutOfRangeException(nameof(at), at, "Invalid at, it should be in the future");
        }

        return await ScheduleCoreAsync(request, type, at, requestContext, cancellationToken);
    }

    private async Task<string> ScheduleCoreAsync<TRequest>(TRequest request, RequestSchedulerType type, DateTimeOffset at,
        IRequestContext? requestContext, CancellationToken cancellationToken)
        where TRequest : class, IRequest
    {
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
                    RequestData = JsonSerializer.Serialize(request, JsonSerialisationOptions.Options),
                    RequestContextData = ScheduledRequestContext.Serialize(requestContext)
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
    public Task<string> ScheduleAsync<TRequest>(TRequest request, RequestSchedulerType type, TimeSpan delay,
        CancellationToken cancellationToken = default) where TRequest : class, IRequest
        => ScheduleAsync(request, type, delay, null, cancellationToken);

    /// <inheritdoc />
    public async Task<string> ScheduleAsync<TRequest>(TRequest request, RequestSchedulerType type, TimeSpan delay,
        IRequestContext? requestContext, CancellationToken cancellationToken)
        where TRequest : class, IRequest
    {
        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), delay, "Invalid delay, it can't be negative");
        }

        return await ScheduleCoreAsync(request, type, timeProvider.GetUtcNow().Add(delay), requestContext, cancellationToken);
    }

    /// <inheritdoc cref="IAmAMessageSchedulerAsync.ReSchedulerAsync(string,System.DateTimeOffset,System.Threading.CancellationToken)"/>
    public async Task<bool> ReSchedulerAsync(string schedulerId, DateTimeOffset at,
        CancellationToken cancellationToken = default)
    {
        if (at < timeProvider.GetUtcNow())
        {
            throw new ArgumentOutOfRangeException(nameof(at), at, "Invalid at, it should be in the future");
        }

        return await ReSchedulerCoreAsync(schedulerId, at, cancellationToken);
    }

    private async Task<bool> ReSchedulerCoreAsync(string schedulerId, DateTimeOffset at,
        CancellationToken cancellationToken)
    {
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

        return await ReSchedulerCoreAsync(schedulerId, timeProvider.GetUtcNow().Add(delay), cancellationToken);
    }

    /// <inheritdoc cref="IAmAMessageSchedulerAsync.CancelAsync"/>
    public async Task CancelAsync(string id, CancellationToken cancellationToken = default)
        => await scheduler.DeleteJob(new JobKey(id, group!), cancellationToken);
}
