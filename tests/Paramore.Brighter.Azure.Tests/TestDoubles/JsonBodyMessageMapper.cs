using System.Text.Json;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.JsonConverters;

namespace Paramore.Brighter.Azure.Tests.TestDoubles;

public class JsonBodyMessageMapper<T> : IAmAMessageMapper<T> where T : class, IRequest
{
    public IRequestContext Context { get; set; } = null!;

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
