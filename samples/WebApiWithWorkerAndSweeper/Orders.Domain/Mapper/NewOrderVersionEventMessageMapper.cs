using System.Text.Json;
using Orders.Domain.Events;
using Paramore.Brighter;

namespace Orders.Domain.Mapper;

public class NewOrderVersionEventMessageMapper : IAmAMessageMapper<NewOrderVersionEvent>
{
    public Message MapToMessage(NewOrderVersionEvent request)
    {
        var header = new MessageHeader(messageId: request.Id, topic: NewOrderVersionEvent.Topic, messageType: MessageType.MT_EVENT);
        var body = new MessageBody(JsonSerializer.Serialize(request, JsonSerialisationOptions.Options));
        var message = new Message(header, body);
        return message;
    }

    public NewOrderVersionEvent MapToRequest(Message message)
    {
        return JsonSerializer.Deserialize<NewOrderVersionEvent>(message.Body.Value, JsonSerialisationOptions.Options);
    }
}
