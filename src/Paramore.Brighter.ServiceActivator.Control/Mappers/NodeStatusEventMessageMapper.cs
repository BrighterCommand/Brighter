using System.Text.Json;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.ServiceActivator.Control.Events;

namespace Paramore.Brighter.ServiceActivator.Control.Mappers;

public class NodeStatusEventMessageMapper : IAmAMessageMapper<NodeStatusEvent>
{
    private readonly string topicName = "control.heartbeat";

    public IRequestContext? Context { get; set; } = null!;

    public Message MapToMessage(NodeStatusEvent request, Publication publication)
    {
        var header = new MessageHeader(messageId: request.Id, topic: new RoutingKey(topicName), messageType: MessageType.MT_EVENT);
        header.Bag["NodeName"] = request.NodeName;
        var body = new MessageBody(JsonSerializer.Serialize(request, JsonSerialisationOptions.Options));
        var message = new Message(header, body);
        return message;
    }

    public NodeStatusEvent MapToRequest(Message message)
    {
#pragma warning disable CS8603 // Possible null reference return.
        return JsonSerializer.Deserialize<NodeStatusEvent>(message.Body.Value, JsonSerialisationOptions.Options);
#pragma warning restore CS8603 // Possible null reference return.
    }
}
