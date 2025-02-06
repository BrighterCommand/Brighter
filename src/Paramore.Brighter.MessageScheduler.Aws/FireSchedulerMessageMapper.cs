using System.Text.Json;
using Paramore.Brighter.Scheduler.Events;

namespace Paramore.Brighter.MessageScheduler.Aws;

/// <summary>
/// The fired scheduler message.
/// </summary>
public class FireSchedulerMessageMapper : IAmAMessageMapper<FireSchedulerMessage>
{
    public IRequestContext? Context { get; set; }
    public Message MapToMessage(FireSchedulerMessage request, Publication publication)
    {
        throw new NotImplementedException();
    }

    public FireSchedulerMessage MapToRequest(Message message)
    {
        return JsonSerializer.Deserialize<FireSchedulerMessage>(message.Body.Value, JsonSerialisationOptions.Options)!;
    }
}
