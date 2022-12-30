using System;
using System.Text.Json;
using Paramore.Brighter.Transforms.Attributes;

namespace Paramore.Brighter.AWS.Tests.TestDoubles;

public class MyLargeCommandMessageMapper : IAmAMessageMapper<MyLargeCommand>
{
    [ClaimCheck(0, thresholdInKb: 5)]
    public Message MapToMessage(MyLargeCommand request)
    {
        return new Message(
            new MessageHeader(request.Id, "transform.event", MessageType.MT_COMMAND, DateTime.UtcNow),
            new MessageBody(JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.General)))
            );
    }

    [RetrieveClaim(0, retain:false)]
    public MyLargeCommand MapToRequest(Message message)
    {
        return JsonSerializer.Deserialize<MyLargeCommand>(message.Body.Value);
    }
}
