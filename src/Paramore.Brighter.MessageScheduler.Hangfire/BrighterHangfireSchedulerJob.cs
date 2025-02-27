using System.Text.Json;
using Paramore.Brighter.Scheduler.Events;

namespace Paramore.Brighter.MessageScheduler.Hangfire;

/// <summary>
/// The Brighter Hangfire job to publish scheduler request/message
/// </summary>
public class BrighterHangfireSchedulerJob(IAmACommandProcessor processor)
{
    /// <summary>
    /// Fire the scheduler message
    /// </summary>
    /// <param name="data">The <see cref="FireSchedulerMessage"/> serialized in JSON format</param>
    public async Task FireSchedulerMessageAsync(string data)
    {
        var scheduler = JsonSerializer.Deserialize<FireSchedulerMessage>(data, JsonSerialisationOptions.Options)!;
        await processor.SendAsync(scheduler);

        var a = new object();
        Console.Write(a.ToString());
    }

    /// <summary>
    /// Fire the scheduler request
    /// </summary>
    /// <param name="data">The <see cref="FireSchedulerRequest"/> serialized in JSON format</param>
    public async Task FireSchedulerRequestAsync(string data)
    {
        var scheduler = JsonSerializer.Deserialize<FireSchedulerRequest>(data, JsonSerialisationOptions.Options)!;
        await processor.SendAsync(scheduler);
    }
}
