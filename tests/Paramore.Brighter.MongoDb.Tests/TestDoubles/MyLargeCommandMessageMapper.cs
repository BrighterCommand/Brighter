using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.MongoDb.Tests.TestDoubles;
using Paramore.Brighter.Transforms.Attributes;

namespace Paramore.Brighter.AWS.Tests.TestDoubles;

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
        return JsonSerializer.Deserialize<MyLargeCommand>(message.Body.Value);
    }
}

public class MyLargeCommandMessageMapperAsync : IAmAMessageMapperAsync<MyLargeCommand>
{
    public IRequestContext Context { get; set; }

    [ClaimCheck(0, thresholdInKb: 5)]
    public async Task<Message> MapToMessageAsync(MyLargeCommand request, Publication publication, CancellationToken cancellationToken = default)
    {
        using var memoryContentStream = new MemoryStream();
        await JsonSerializer.SerializeAsync(memoryContentStream, request, new JsonSerializerOptions(JsonSerializerDefaults.General), cancellationToken);
        return new Message(
            new MessageHeader(request.Id, publication.Topic, request.RequestToMessageType(), timeStamp: DateTime.UtcNow),
            new MessageBody(memoryContentStream.ToArray()));
    }

    [RetrieveClaim(0, retain:false)]
    public async Task<MyLargeCommand> MapToRequestAsync(Message message, CancellationToken cancellationToken = default)
    {
        using MemoryStream stream = new(message.Body.Bytes);
        return await JsonSerializer.DeserializeAsync<MyLargeCommand>(stream, cancellationToken: cancellationToken);
    }
}
