using System.Text.Json;
using Paramore.Brighter.JsonConverters;

namespace Paramore.Brighter.MessageScheduler.AWS.V4;

/// <summary>
/// The command to fire scheduler (message and request) on AWS scheduler.
/// </summary>
public class FireAwsScheduler() : Command(Id.Random)
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
/// The <see cref="FireAwsScheduler"/>
/// </summary>
public class AwsSchedulerFiredMapper : IAmAMessageMapper<FireAwsScheduler>
{
    /// <inheritdoc />
    public IRequestContext? Context { get; set; }

    /// <inheritdoc />
    public Message MapToMessage(FireAwsScheduler request, Publication publication)
    {
        return new Message
        {
            Header =
                new MessageHeader(request.Id, publication.Topic!, MessageType.MT_EVENT,
                    subject: nameof(FireAwsScheduler)),
            Body = new MessageBody(JsonSerializer.Serialize(request, JsonSerialisationOptions.Options))
        };
    }

    /// <inheritdoc />
    public FireAwsScheduler MapToRequest(Message message) 
        => JsonSerializer.Deserialize<FireAwsScheduler>(message.Body.Value, JsonSerialisationOptions.Options)!;
}
