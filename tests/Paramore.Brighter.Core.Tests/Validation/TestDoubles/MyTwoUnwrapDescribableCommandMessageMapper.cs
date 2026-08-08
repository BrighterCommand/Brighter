using System;
using System.Text.Json;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Transforms.Attributes;

namespace Paramore.Brighter.Core.Tests.Validation.TestDoubles;

/// <summary>
/// A mapper declaring two distinct unwrap transforms — <see cref="MyDescribableUnwrapWith"/>
/// (→ <see cref="MyDescribableTransform"/>) and <c>[Decompress]</c> (→ <c>CompressPayloadTransformer</c>) —
/// used to verify that a resolvable unwrap transform does not suppress a warning for an unresolvable one.
/// </summary>
public class MyTwoUnwrapDescribableCommandMessageMapper : IAmAMessageMapper<MyDescribableCommand>
{
    public IRequestContext? Context { get; set; }

    public Message MapToMessage(MyDescribableCommand request, Publication publication)
    {
        return new Message(
            new MessageHeader(request.Id, publication.Topic, request.RequestToMessageType(), timeStamp: DateTime.UtcNow),
            new MessageBody(JsonSerializer.Serialize(request, JsonSerializerOptions.Default))
        );
    }

    [MyDescribableUnwrapWith(1)]
    [Decompress(0)]
    public MyDescribableCommand MapToRequest(Message message)
    {
        return JsonSerializer.Deserialize<MyDescribableCommand>(message.Body.Value)!;
    }
}
