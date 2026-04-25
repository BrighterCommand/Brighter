using System;
using System.Text.Json;
using Paramore.Brighter.Extensions;

namespace Paramore.Brighter.Core.Tests.Validation.TestDoubles;

public class MyDescribableCommandMessageMapper : IAmAMessageMapper<MyDescribableCommand>
{
    public IRequestContext? Context { get; set; }

    [MyDescribableWrapWith(0)]
    public Message MapToMessage(MyDescribableCommand request, Publication publication)
    {
        return new Message(
            new MessageHeader(request.Id, publication.Topic, request.RequestToMessageType(), timeStamp: DateTime.UtcNow),
            new MessageBody(JsonSerializer.Serialize(request, JsonSerializerOptions.Default))
        );
    }

    [MyDescribableUnwrapWith(0)]
    public MyDescribableCommand MapToRequest(Message message)
    {
        return JsonSerializer.Deserialize<MyDescribableCommand>(message.Body.Value)!;
    }
}
