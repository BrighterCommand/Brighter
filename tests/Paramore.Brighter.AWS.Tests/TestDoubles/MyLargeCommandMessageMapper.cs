using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
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

public class MyLargeCommandMessageMapperAsync : IAmAMessageMapperAsync<MyLargeCommand>
{
    [ClaimCheck(0, thresholdInKb: 5)]
    public async Task<Message> MapToMessage(MyLargeCommand request)
    {
        using var memoryContentStream = new MemoryStream();
        await JsonSerializer.SerializeAsync(memoryContentStream, request, new JsonSerializerOptions(JsonSerializerDefaults.General));
        return new Message(
            new MessageHeader(request.Id, "transform.event", MessageType.MT_COMMAND, DateTime.UtcNow),
            new MessageBody(memoryContentStream.ToArray()));
    }

    [RetrieveClaim(0, retain:false)]
    public async Task<MyLargeCommand> MapToRequest(Message message)
    {
        using MemoryStream stream = new(message.Body.Bytes);
        return await JsonSerializer.DeserializeAsync<MyLargeCommand>(stream);
    }
}
