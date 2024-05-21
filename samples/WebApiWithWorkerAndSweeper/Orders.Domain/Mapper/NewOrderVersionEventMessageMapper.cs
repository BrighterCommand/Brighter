using System.Text.Json;
using Orders.Domain.Events;
using Paramore.Brighter;

namespace Orders.Domain.Mapper;

public class NewOrderVersionEventMessageMapper : IAmAMessageMapper<NewOrderVersionEvent>
{
    public IRequestContext Context { get; set; }

    public Message MapToMessage(NewOrderVersionEvent request, Publication publication)
    {
        var header = new MessageHeader(messageId: request.Id, topic: publication.Topic, messageType: MessageType.MT_EVENT);
        var body = new MessageBody(JsonSerializer.Serialize(request, JsonSerialisationOptions.Options));
        var message = new Message(header, body);
        return message;
    }

    public NewOrderVersionEvent MapToRequest(Message message)
    {
#pragma warning disable CS8603 // Possible null reference return.
        return JsonSerializer.Deserialize<NewOrderVersionEvent>(message.Body.Value, JsonSerialisationOptions.Options);
#pragma warning restore CS8603 // Possible null reference return.
    }
}
