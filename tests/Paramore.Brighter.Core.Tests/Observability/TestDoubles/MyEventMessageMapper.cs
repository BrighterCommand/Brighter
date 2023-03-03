using System.Text.Json;

namespace Paramore.Brighter.Core.Tests.Observability.TestDoubles;

public class MyEventMessageMapper : IAmAMessageMapper<MyEvent>
{
    public Message MapToMessage(MyEvent request)
    {
        MessageType messageType = MessageType.MT_EVENT;

        var header = new MessageHeader(messageId: request.Id, topic: MyEvent.Topic, messageType: messageType);
        var body = new MessageBody(JsonSerializer.Serialize(request));
        var message = new Message(header, body);
        return message;
    }

    public MyEvent MapToRequest(Message message)
    {
        return JsonSerializer.Deserialize<MyEvent>(message.Body.Value);
    }
}
