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
            return;
        }

        if (!context.JobDetail.JobDataMap.TryGetBooleanValue("async", out var async))
        {
            return;
        }

        var id = context.JobDetail.Key.Name;
        var message = JsonSerializer.Deserialize<Message>(obj!, JsonSerialisationOptions.Options)!;
        if (async)
        {
            await processor.PostAsync(new FireSchedulerMessage { Id = id, Message = message });
        }
        else
        {
            processor.Post(new FireSchedulerMessage { Id = id, Message = message });
        }
    }
}
