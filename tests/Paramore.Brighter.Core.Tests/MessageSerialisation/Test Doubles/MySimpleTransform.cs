using System.Text.Json;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;

public class MySimpleTransform : Transform
{
    public static readonly string HEADER_KEY = "MySimpleTransformTest";
    public static readonly string TRANSFORM_VALUE = "I am a transformed value";

    public override Message Wrap(Message message, Publication publication)
    {
        message.Header.Bag.Add(HEADER_KEY, TRANSFORM_VALUE);
        return message;
    }

    public override Message Unwrap(Message message)
    {
        var oldCommand = JsonSerializer.Deserialize<MyTransformableCommand>(message.Body.Value);
        oldCommand.Value = message.Header.Bag[HEADER_KEY].ToString();
        message.Body = new MessageBody(JsonSerializer.Serialize(oldCommand, new JsonSerializerOptions(JsonSerializerDefaults.General)));
        return message;
    }
}
