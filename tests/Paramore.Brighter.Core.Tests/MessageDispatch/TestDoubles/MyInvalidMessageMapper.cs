using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Actions;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.JsonConverters;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;

public class MyInvalidMessageMapper : IAmAMessageMapper<MyRejectedEvent>, IAmAMessageMapperAsync<MyRejectedEvent>
{
    public const string DeserializationFailureMessage = "Failed to deserialize message";

    public IRequestContext? Context { get; set; }

    public Message MapToMessage(MyRejectedEvent request, Publication publication)
    {
        var header = new MessageHeader(
            messageId: request.Id,
            topic: publication.Topic ?? RoutingKey.Empty,
            source: publication.Source,
            type: publication.Type,
            messageType: request.RequestToMessageType());

        var body = new MessageBody(JsonSerializer.Serialize(request, JsonSerialisationOptions.Options));
        var message = new Message(header, body);
        return message;
    }

    public MyRejectedEvent MapToRequest(Message message)
    {
        // Simulate deserialization failure by throwing InvalidMessageAction
        throw new InvalidMessageAction(DeserializationFailureMessage);
    }

    public Task<Message> MapToMessageAsync(MyRejectedEvent request, Publication publication,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(MapToMessage(request, publication));
    }

    public Task<MyRejectedEvent> MapToRequestAsync(Message message,
        CancellationToken cancellationToken = default)
    {
        // Simulate deserialization failure by throwing InvalidMessageAction
        throw new InvalidMessageAction(DeserializationFailureMessage);
    }
}
