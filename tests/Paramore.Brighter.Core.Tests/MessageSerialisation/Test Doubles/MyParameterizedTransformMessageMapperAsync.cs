using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Threading.Tasks;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;

public class MyParameterizedTransformMessageMapperAsync: IAmAMessageMapperAsync<MyTransformableCommand>
{
    [MyParameterizedWrapWith(0,  displayFormat: "I am a format indicator {0}" )]
    public Task<Message> MapToMessage(MyTransformableCommand request)
    {
        var tcs = new TaskCompletionSource<Message>();
        tcs.SetResult(new Message(
            new MessageHeader(request.Id, "transform.event", MessageType.MT_COMMAND, DateTime.UtcNow),
            new MessageBody(JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.General)))
        ));
        return tcs.Task;
    }                                                       

    [MyParameterizedUnwrapWith(0, template: "I am a parameterized template: {0}")]
    public Task<MyTransformableCommand> MapToRequest(Message message)
    {
        var tcs = new TaskCompletionSource<MyTransformableCommand>();
        tcs.SetResult(JsonSerializer.Deserialize<MyTransformableCommand>(message.Body.Value));
        return tcs.Task;
    }
}
