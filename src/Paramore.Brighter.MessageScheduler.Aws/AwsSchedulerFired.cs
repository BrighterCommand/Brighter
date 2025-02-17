using System.Text.Json;

namespace Paramore.Brighter.MessageScheduler.Aws;

public class AwsSchedulerFired() : Event(Guid.NewGuid())
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
/// The <see cref="AwsSchedulerFired"/>
/// </summary>
public class AwsSchedulerFiredMapper : IAmAMessageMapper<AwsSchedulerFired>
{
    /// <inheritdoc />
    public IRequestContext? Context { get; set; }

    /// <inheritdoc />
    public Message MapToMessage(AwsSchedulerFired request, Publication publication)
    {
        return new Message
        {
            Header =
                new MessageHeader(request.Id, publication.Topic!, MessageType.MT_EVENT,
                    subject: nameof(AwsSchedulerFired)),
            Body = new MessageBody(JsonSerializer.Serialize(request, JsonSerialisationOptions.Options))
        };
    }

    /// <inheritdoc />
    public AwsSchedulerFired MapToRequest(Message message) 
        => JsonSerializer.Deserialize<AwsSchedulerFired>(message.Body.Value, JsonSerialisationOptions.Options)!;
}
