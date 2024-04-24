using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Transforms.Attributes;

namespace Paramore.Brighter.Core.Tests.Claims.Test_Doubles;

public class MyLargeCommandMessageMapperAsync : IAmAMessageMapperAsync<MyLargeCommand>
{
    [ClaimCheck(0, thresholdInKb: 5)]
    public async Task<Message> MapToMessageAsync(MyLargeCommand request, Publication publication, CancellationToken cancellationToken = default)
    {                                                                        
        using MemoryStream stream = new();
        await JsonSerializer.SerializeAsync(stream, request, new JsonSerializerOptions(JsonSerializerDefaults.General), cancellationToken);
        return new Message(
            new MessageHeader(request.Id, publication.Topic, request.RequestToMessageType(), timeStamp: DateTime.UtcNow),
            new MessageBody(stream.ToArray()));
    }

    [RetrieveClaim(0, retain:false)]
    public async Task<MyLargeCommand> MapToRequestAsync(Message message, CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream(message.Body.Bytes);
        stream.Position = 0;
        return await JsonSerializer.DeserializeAsync<MyLargeCommand>(stream, cancellationToken:cancellationToken);
    }
}
