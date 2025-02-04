using Paramore.Brighter.Tasks;
using Quartz;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Paramore.Brighter.MessageScheduler.Quartz;

/// <summary>
/// The Quartz Message scheduler
/// </summary>
/// <param name="scheduler"></param>
/// <param name="group"></param>
/// <param name="getOrCreateSchedulerId"></param>
public class QuartzMessageScheduler(IScheduler scheduler, string? group, Func<Message, string> getOrCreateSchedulerId)
    : IAmAMessageSchedulerSync, IAmAMessageSchedulerAsync
{
    /// <inheritdoc />
    public string Schedule(Message message, DateTimeOffset at)
    {
        var id = getOrCreateSchedulerId(message);
        var job = JobBuilder.Create<QuartzBrighterJob>()
            .WithIdentity(getOrCreateSchedulerId(message), group!)
            .UsingJobData("message", JsonSerializer.Serialize(message, JsonSerialisationOptions.Options))
            .UsingJobData("async", false)
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity(getOrCreateSchedulerId(message) + "-trigger", group!)
            .StartAt(at)
            .Build();

        BrighterAsyncContext.Run(async () => await scheduler.ScheduleJob(job, trigger));
        return id;
    }

    /// <inheritdoc />
    public string Schedule(Message message, TimeSpan delay) => Schedule(message, DateTimeOffset.Now.Add(delay));

    /// <inheritdoc />
    public bool ReScheduler(string schedulerId, DateTimeOffset at)
        => BrighterAsyncContext.Run(async () => await ReSchedulerAsync(schedulerId, at));

    /// <inheritdoc />
    public bool ReScheduler(string schedulerId, TimeSpan delay) =>
        ReScheduler(schedulerId, DateTimeOffset.Now.Add(delay));

    /// <inheritdoc />
    public void Cancel(string id)
        => BrighterAsyncContext.Run(async () => await CancelAsync(id));

    /// <inheritdoc />
    public async Task<string> ScheduleAsync(Message message, DateTimeOffset at,
        CancellationToken cancellationToken = default)
    {
        var id = getOrCreateSchedulerId(message);
        var job = JobBuilder.Create<QuartzBrighterJob>()
            .WithIdentity(getOrCreateSchedulerId(message), group!)
            .UsingJobData("message", JsonSerializer.Serialize(message, JsonSerialisationOptions.Options))
            .UsingJobData("async", true)
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity(getOrCreateSchedulerId(message) + "-trigger", group!)
            .StartAt(at)
            .Build();

        await scheduler.ScheduleJob(job, trigger, cancellationToken);
        return id;
    }

    /// <inheritdoc />
    public async Task<string> ScheduleAsync(Message message, TimeSpan delay,
        CancellationToken cancellationToken = default)
        => await ScheduleAsync(message, DateTimeOffset.Now.Add(delay), cancellationToken);

    /// <inheritdoc />
    public async Task<bool> ReSchedulerAsync(string schedulerId, DateTimeOffset at,
        CancellationToken cancellationToken = default)
    {
        var date = await scheduler.RescheduleJob(new TriggerKey(schedulerId + "-trigger", group!), TriggerBuilder
            .Create()
            .WithIdentity(schedulerId + "-trigger", group!)
            .StartAt(at)
            .Build(), cancellationToken);

        return date != null;
    }

    /// <inheritdoc />
    public async Task<bool> ReSchedulerAsync(string schedulerId, TimeSpan delay,
        CancellationToken cancellationToken = default) =>
        await ReSchedulerAsync(schedulerId, DateTimeOffset.Now.Add(delay), cancellationToken);

    /// <inheritdoc />
    public async Task CancelAsync(string id, CancellationToken cancellationToken = default)
        => await scheduler.DeleteJob(new JobKey(id, group!), cancellationToken);


    /// <inheritdoc />
    public ValueTask DisposeAsync() => new();

    /// <inheritdoc />
    public void Dispose()
    {
    }
}
