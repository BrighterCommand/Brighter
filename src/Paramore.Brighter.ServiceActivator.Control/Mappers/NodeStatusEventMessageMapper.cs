﻿using System.Text.Json;
using Paramore.Brighter.ServiceActivator.Control.Events;

namespace Paramore.Brighter.ServiceActivator.Control.Mappers;

public class NodeStatusEventMessageMapper : IAmAMessageMapper<NodeStatusEvent>
{
    private readonly string topicName = "control.heartbeat";
    
    public Message MapToMessage(NodeStatusEvent request)
    {
        var header = new MessageHeader(messageId: request.Id, topic: topicName, messageType: MessageType.MT_EVENT);
        header.Bag["NodeName"] = request.NodeName;
        var body = new MessageBody(JsonSerializer.Serialize(request, JsonSerialisationOptions.Options));
        var message = new Message(header, body);
        return message;
    }

    public NodeStatusEvent MapToRequest(Message message)
    {
        return JsonSerializer.Deserialize<NodeStatusEvent>(message.Body.Value, JsonSerialisationOptions.Options);
    }
}
