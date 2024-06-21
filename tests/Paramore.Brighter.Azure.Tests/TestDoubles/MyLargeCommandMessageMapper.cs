using System;
using System.Text.Json;
using Paramore.Brighter.Azure.Tests.TestDoubles;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Transforms.Attributes;

namespace Paramore.Brighter.Azure.Tests.TestDoubles;

public class MyLargeCommandMessageMapper : IAmAMessageMapper<MyLargeCommand>
{
    public IRequestContext Context { get; set; }

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
#pragma warning disable CS8603 // Possible null reference return.
        return JsonSerializer.Deserialize<MyLargeCommand>(message.Body.Value);
#pragma warning restore CS8603 // Possible null reference return.
    }
}
