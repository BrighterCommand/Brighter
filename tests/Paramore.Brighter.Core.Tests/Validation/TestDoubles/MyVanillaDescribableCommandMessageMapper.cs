using System;
using System.Text.Json;
using Paramore.Brighter.Extensions;

namespace Paramore.Brighter.Core.Tests.Validation.TestDoubles;

/// <summary>
/// A mapper with no wrap/unwrap attributes — used to test the "vanilla" (no transforms) path.
/// </summary>
public class MyVanillaDescribableCommandMessageMapper : IAmAMessageMapper<MyDescribableCommand>
{
    public IRequestContext? Context { get; set; }

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
