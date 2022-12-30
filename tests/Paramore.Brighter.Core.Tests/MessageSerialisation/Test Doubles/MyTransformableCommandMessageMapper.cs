using System;
using System.Text.Json;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;

public class MyTransformableCommandMessageMapper : IAmAMessageMapper<MyTransformableCommand>
{
    [MySimpleWrapWith(0)]
    public Message MapToMessage(MyTransformableCommand request)
    {
        return new Message(
            new MessageHeader(request.Id, "transform.event", MessageType.MT_COMMAND, DateTime.UtcNow),
            new MessageBody(JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.General)))
            );
    }

    [MySimpleUnwrapWith(0)]
    public MyTransformableCommand MapToRequest(Message message)
    {
        return JsonSerializer.Deserialize<MyTransformableCommand>(message.Body.Value);
    }
}
