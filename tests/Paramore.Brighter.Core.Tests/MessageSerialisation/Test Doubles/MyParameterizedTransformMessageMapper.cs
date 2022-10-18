using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;

public class MyParameterizedTransformMessageMapper: IAmAMessageMapper<MyTransformableCommand>
{
    [MyParameterizedWrapWith(0,  displayFormat: "I am a format indicator {0}" )]
    public Message MapToMessage(MyTransformableCommand request)
    {
        return new Message(
            new MessageHeader(request.Id, "transform.event", MessageType.MT_COMMAND, DateTime.UtcNow),
            new MessageBody(JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.General)))
        );
    }                                                       

    [MyParameterizedUnwrapWith(0, template: "I am a parameterized header template: {0}")]
    public MyTransformableCommand MapToRequest(Message message)
    {
        var command = JsonSerializer.Deserialize<MyTransformableCommand>(message.Body.Value);
        command.Value = string.Format(message.Header.Bag[MyParameterizedTransformAsync.HEADER_KEY].ToString(), command.Value);
        return command;
    }
}
