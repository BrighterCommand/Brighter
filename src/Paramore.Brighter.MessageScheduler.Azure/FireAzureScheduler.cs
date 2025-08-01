using System.Text.Json;
using Paramore.Brighter.JsonConverters;

namespace Paramore.Brighter.MessageScheduler.Azure;

/// <summary>
/// The command to fire scheduler (message and request) on Azure service bus.
/// </summary>
public class FireAzureScheduler() : Command(Id.Random())
{
    /// <summary>
    /// The <see cref="RequestSchedulerType"/>
    /// </summary>
    public RequestSchedulerType SchedulerType { get; set; }

    /// <summary>
    /// The request type
    /// </summary>
    public string? RequestType { get; set; }

    /// <summary>
    /// The request data
    /// </summary>
    public string? RequestData { get; set; }

    /// <summary>
    /// The message that will be fire
    /// </summary>
    public Message? Message { get; set; }

    /// <summary>
    /// If it should post sync or async
    /// </summary>
    public bool Async { get; set; }
}

/// <summary>
/// The <see cref="FireAzureScheduler"/>
/// </summary>
public class AzureSchedulerFiredMapper : IAmAMessageMapper<FireAzureScheduler>
{
    /// <inheritdoc />
    public IRequestContext? Context { get; set; }

    /// <inheritdoc />
    public Message MapToMessage(FireAzureScheduler request, Publication publication)
    {
        return new Message
        {
            Header =
                new MessageHeader(request.Id, publication.Topic!, MessageType.MT_EVENT,
                    subject: nameof(FireAzureScheduler)),
            Body = new MessageBody(JsonSerializer.Serialize(request, JsonSerialisationOptions.Options))
        };
    }

    /// <inheritdoc />
    public FireAzureScheduler MapToRequest(Message message) 
        => JsonSerializer.Deserialize<FireAzureScheduler>(message.Body.Value, JsonSerialisationOptions.Options)!;
}
