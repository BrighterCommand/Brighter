using System;
using System.Text.Json;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Transforms.Attributes;

namespace Paramore.Brighter.Core.Tests.Validation.TestDoubles;

/// <summary>
/// A mapper declaring two distinct wrap transforms — <see cref="MyDescribableWrapWith"/>
/// (→ <see cref="MyDescribableTransform"/>) and <c>[Compress]</c> (→ <c>CompressPayloadTransformer</c>) —
/// used to verify that a resolvable transform does not suppress a warning for an unresolvable one.
/// </summary>
public class MyTwoWrapDescribableCommandMessageMapper : IAmAMessageMapper<MyDescribableCommand>
{
    public IRequestContext? Context { get; set; }

    [MyDescribableWrapWith(1)]
    [Compress(0)]
    public Message MapToMessage(MyDescribableCommand request, Publication publication)
    {
        return new Message(
            new MessageHeader(request.Id, publication.Topic, request.RequestToMessageType(), timeStamp: DateTime.UtcNow),
            new MessageBody(JsonSerializer.Serialize(request, JsonSerializerOptions.Default))
        );
    }

    public MyDescribableCommand MapToRequest(Message message)
    {
        return JsonSerializer.Deserialize<MyDescribableCommand>(message.Body.Value)!;
    }
}
