using System;
using System.Text.Json;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Transforms.Attributes;

namespace Paramore.Brighter.Core.Tests.Claims.Test_Doubles;

public class MyLargeCommandMessageMapper : IAmAMessageMapper<MyLargeCommand>
{
    [ClaimCheck(0, thresholdInKb: 5)]
    public Message MapToMessage(MyLargeCommand request, Publication publication)
    {
        return new Message(
            new MessageHeader(request.Id, publication.Topic, request.RequestToMessageType(), timeStamp: DateTime.UtcNow),
            new MessageBody(JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.General)))
            );
    }

    [RetrieveClaim(0, retain:false)]
    public MyLargeCommand MapToRequest(Message message)
    {
        return JsonSerializer.Deserialize<MyLargeCommand>(message.Body.Value);
    }
}
