using System;
using System.Text.Json;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;

public class MyParameterizedTransformMessageMapper: IAmAMessageMapper<MyTransformableCommand>
{
    public IRequestContext Context { get; set; }

    [MyParameterizedWrapWith(0,  displayFormat: "I am a format indicator {0}" )]
    public Message MapToMessage(MyTransformableCommand request, Publication publication)
    {
        return new Message(
            new MessageHeader(request.Id, new("transform.event"), MessageType.MT_COMMAND, timeStamp: DateTime.UtcNow),
            new MessageBody(JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.General)))
        );
    }                                                       

    [MyParameterizedUnwrapWith(0, template: "I am a parameterized template: {0}")]
    public MyTransformableCommand MapToRequest(Message message)
    {
        return JsonSerializer.Deserialize<MyTransformableCommand>(message.Body.Value);
    }
}
