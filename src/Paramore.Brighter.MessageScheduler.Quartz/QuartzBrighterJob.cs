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
        if (!context.JobDetail.JobDataMap.TryGetString("message", out var obj))
        {
            throw new InvalidOperationException("Not message, something is wrong with this job scheduler");
        }

        var fireScheduler = JsonSerializer.Deserialize<FireSchedulerMessage>(obj!, JsonSerialisationOptions.Options)!;
        await processor.SendAsync(fireScheduler);
    }
}
