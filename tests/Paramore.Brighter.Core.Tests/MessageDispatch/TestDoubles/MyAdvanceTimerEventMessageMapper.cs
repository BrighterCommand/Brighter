using System.Text.Json;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.JsonConverters;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;

public class MyAdvanceTimerEventMessageMapper : IAmAMessageMapper<MyAdvanceTimerEvent>
{
    public IRequestContext? Context { get; set; }
    
    public Message MapToMessage(MyAdvanceTimerEvent request, Publication publication)
    {
        var header = new MessageHeader(messageId: request.Id, topic: publication.Topic ?? RoutingKey.Empty, source: publication.Source, 
            type: publication.Type, messageType: request.RequestToMessageType());
        var body = new MessageBody(JsonSerializer.Serialize(request, JsonSerialisationOptions.Options));
        var message = new Message(header, body);
        return message;
    }

    public MyAdvanceTimerEvent MapToRequest(Message message)
    {
        return JsonSerializer.Deserialize<MyAdvanceTimerEvent>(message.Body.Value, JsonSerialisationOptions.Options)!;
    }
}
