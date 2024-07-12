using System.Text.Json;
using OpenTelemetry.Shared.Commands;
using OpenTelemetry.Shared.Events;
using Paramore.Brighter;
using Paramore.Brighter.Extensions;

namespace OpenTelemetry.Shared.Mappers;

public class MessageMapper<T> : IAmAMessageMapper<T> where T : class, IRequest
{
    public IRequestContext Context { get; set; }

    public Message MapToMessage(T request, Publication publication)
    {
        var header = new MessageHeader(messageId: request.Id, topic: publication.Topic, messageType: request.RequestToMessageType());
        var body = new MessageBody(JsonSerializer.Serialize(request));
        var message = new Message(header, body);
        return message;
    }

 

    public T MapToRequest(Message message)
    {
#pragma warning disable CS8603 // Possible null reference return.
        return JsonSerializer.Deserialize<T>(message.Body.Value, JsonSerialisationOptions.Options);
#pragma warning restore CS8603 // Possible null reference return.
    }
}

