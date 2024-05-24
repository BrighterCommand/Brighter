﻿using System.Text.Json;
using Paramore.Brighter.Extensions;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;

internal class MyEventMessageMapper : IAmAMessageMapper<MyEvent>
{
    public IRequestContext Context { get; set; }

    public Message MapToMessage(MyEvent request, Publication publication)
    {
        var header = new MessageHeader(messageId: request.Id, topic: publication.Topic, messageType: request.RequestToMessageType());
        var body = new MessageBody(JsonSerializer.Serialize(request, JsonSerialisationOptions.Options));
        var message = new Message(header, body);
        return message;
    }

    public MyEvent MapToRequest(Message message)
    {
        return JsonSerializer.Deserialize<MyEvent>(message.Body.Value, JsonSerialisationOptions.Options);
    }
}

