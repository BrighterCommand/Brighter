using System;
using System.Text.Json;
using Paramore.Brighter.Scheduler.Events;

namespace Paramore.Brighter.Scheduler.Mappers;

/// <summary>
/// The default mapper for <see cref="FireSchedulerRequest"/>
/// </summary>
public class FireSchedulerRequestMapper : IAmAMessageMapper<FireSchedulerRequest>
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
    public Message MapToMessage(FireSchedulerRequest request, Publication publication)
        => throw new NotImplementedException();

    /// <inheritdoc />
    public FireSchedulerRequest MapToRequest(Message message)
        => JsonSerializer.Deserialize<FireSchedulerRequest>(message.Body.Value, JsonSerialisationOptions.Options)!;
}
