using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;

public class MyTransformableCommandMessageMapperAsync : IAmAMessageMapperAsync<MyTransformableCommand>
{
    [MySimpleWrapWith(0)]
    public Task<Message> MapToMessage(MyTransformableCommand request)
    {
        var tcs = new TaskCompletionSource<Message>();
        tcs.SetResult(new Message(
            new MessageHeader(request.Id, "transform.event", MessageType.MT_COMMAND, DateTime.UtcNow),
            new MessageBody(JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.General)))
            ));
        return tcs.Task;
    }

    [MySimpleUnwrapWith(0)]
    public Task<MyTransformableCommand> MapToRequest(Message message)
    {
        var tcs = new TaskCompletionSource<MyTransformableCommand>();
        tcs.SetResult(JsonSerializer.Deserialize<MyTransformableCommand>(message.Body.Value));
        return tcs.Task;
    }
}
