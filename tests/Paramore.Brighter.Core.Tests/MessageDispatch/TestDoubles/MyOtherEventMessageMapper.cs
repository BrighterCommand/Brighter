using System.Text.Json;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.JsonConverters;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;

internal sealed class MyOtherEventMessageMapper : IAmAMessageMapper<MyOtherEvent>
{
    public IRequestContext Context { get; set; }

    public Message MapToMessage(MyOtherEvent request, Publication publication)
    {
        var header = new MessageHeader(messageId: request.Id, topic: publication.Topic ?? RoutingKey.Empty, source: publication.Source, 
            type: publication.Type, messageType: request.RequestToMessageType());
        var body = new MessageBody(JsonSerializer.Serialize(request, JsonSerialisationOptions.Options));
        var message = new Message(header, body);
        return message;
    }

    public MyOtherEvent MapToRequest(Message message)
    {
        return JsonSerializer.Deserialize<MyOtherEvent>(message.Body.Value, JsonSerialisationOptions.Options)!;
    }
}

