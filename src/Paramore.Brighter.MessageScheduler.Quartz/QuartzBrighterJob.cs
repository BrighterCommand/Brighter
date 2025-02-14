using System.Text.Json;
using Paramore.Brighter.Scheduler.Events;
using Quartz;

namespace Paramore.Brighter.MessageScheduler.Quartz;

/// <summary>
/// The Quartz Message scheduler Job
/// </summary>
/// <param name="processor"></param>
public class QuartzBrighterJob(IAmACommandProcessor processor) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        if (context.JobDetail.JobDataMap.TryGetString("message", out var message))
        {
            var scheduler = JsonSerializer.Deserialize<FireSchedulerMessage>(message!, JsonSerialisationOptions.Options)!;
            await processor.SendAsync(scheduler);
        }

        if (context.JobDetail.JobDataMap.TryGetString("request", out var request))
        {
            var scheduler = JsonSerializer.Deserialize<FireSchedulerRequest>(request!, JsonSerialisationOptions.Options)!;
            await processor.SendAsync(scheduler);
        }

        throw new InvalidOperationException(
            "No message or request found in the job, something is wrong with this job scheduler");
    }
}
