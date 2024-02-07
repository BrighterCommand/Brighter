using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Paramore.Brighter.Transforms.Attributes;

namespace Paramore.Brighter.Core.Tests.Claims.Test_Doubles;

public class MyLargeCommandMessageMapperAsync : IAmAMessageMapperAsync<MyLargeCommand>
{
    [ClaimCheck(0, thresholdInKb: 5)]
    public Task<Message> MapToMessage(MyLargeCommand request)
    {
        var tcs = new TaskCompletionSource<Message>();
        
        tcs.SetResult(new Message(
            new MessageHeader(request.Id, "transform.event", MessageType.MT_COMMAND, DateTime.UtcNow),
            new MessageBody(JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.General)))
            ));

        return tcs.Task;
    }

    [RetrieveClaim(0, retain:false)]
    public async Task<MyLargeCommand> MapToRequest(Message message)
    {
        using var stream = new MemoryStream(message.Body.Bytes);
        stream.Position = 0;
        return await JsonSerializer.DeserializeAsync<MyLargeCommand>(stream);
    }
}
