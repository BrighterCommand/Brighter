using System;
using System.Text.Json;
using Paramore.Brighter.Scheduler.Events;

namespace Paramore.Brighter.Scheduler.Mappers;

/// <summary>
/// The default mapper for <see cref="FireSchedulerMessage"/>
/// </summary>
public class FireSchedulerMessageMapper : IAmAMessageMapper<FireSchedulerMessage>
{
    /// <inheritdoc />
    public IRequestContext? Context { get; set; }

    /// <summary>
    /// We don't need to map it to message
    /// </summary>
    /// <param name="request"></param>
    /// <param name="publication"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public Message MapToMessage(FireSchedulerMessage request, Publication publication)
        => throw new NotImplementedException();

    /// <inheritdoc />
    public FireSchedulerMessage MapToRequest(Message message)
        => JsonSerializer.Deserialize<FireSchedulerMessage>(message.Body.Value, JsonSerialisationOptions.Options)!;
}
