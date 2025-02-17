using System.Text.Json;

namespace Paramore.Brighter.MessageScheduler.Azure;

public class AzureSchedulerFired() : Event(Guid.NewGuid())
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
/// The <see cref="AzureSchedulerFired"/>
/// </summary>
public class AzureSchedulerFiredMapper : IAmAMessageMapper<AzureSchedulerFired>
{
    /// <inheritdoc />
    public IRequestContext? Context { get; set; }

    /// <inheritdoc />
    public Message MapToMessage(AzureSchedulerFired request, Publication publication)
    {
        return new Message
        {
            Header =
                new MessageHeader(request.Id, publication.Topic!, MessageType.MT_EVENT,
                    subject: nameof(AzureSchedulerFired)),
            Body = new MessageBody(JsonSerializer.Serialize(request, JsonSerialisationOptions.Options))
        };
    }

    /// <inheritdoc />
    public AzureSchedulerFired MapToRequest(Message message) 
        => JsonSerializer.Deserialize<AzureSchedulerFired>(message.Body.Value, JsonSerialisationOptions.Options)!;
}
