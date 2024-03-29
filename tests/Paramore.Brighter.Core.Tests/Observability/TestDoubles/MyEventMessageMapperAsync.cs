﻿using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Extensions;

namespace Paramore.Brighter.Core.Tests.Observability.TestDoubles;

public class MyEventMessageMapperAsync : IAmAMessageMapperAsync<MyEvent>
{
    public Task<Message> MapToMessageAsync(MyEvent request, Publication publication, CancellationToken ct = default)
    {
        var header = new MessageHeader(messageId: request.Id, topic: publication.Topic, messageType: request.RequestToMessageType());
        var body = new MessageBody(JsonSerializer.Serialize(request));
        var message = new Message(header, body);
        return Task.FromResult(message);
    }

    public Task<MyEvent> MapToRequestAsync(Message message, CancellationToken ct = default)
    {
        return Task.FromResult(JsonSerializer.Deserialize<MyEvent>(message.Body.Value));
    }
}
