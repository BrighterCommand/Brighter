using System.Text.Json;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.JsonConverters;

namespace Paramore.Brighter.InMemory.Tests.TestDoubles;

public class MyEventMessageMapper : IAmAMessageMapper<MyEvent>
{
    public IRequestContext Context { get; set; }
    public Message MapToMessage(MyEvent request, Publication publication)
    {
        var header = new MessageHeader(messageId: request.Id, topic: publication.Topic, source: publication.Source, 
            type: publication.Type, messageType: request.RequestToMessageType());
        var body = new MessageBody(JsonSerializer.Serialize(request, JsonSerialisationOptions.Options));
        var message = new Message(header, body);
        return message;
    }

    public MyEvent MapToRequest(Message message)
    {
        return JsonSerializer.Deserialize<MyEvent>(message.Body.Value, JsonSerialisationOptions.Options);
    }
}
