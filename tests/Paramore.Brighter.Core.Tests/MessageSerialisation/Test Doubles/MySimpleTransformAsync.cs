using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;

public class MySimpleTransformAsync : TransformAsync
{
    public static readonly string HEADER_KEY = "MySimpleTransformTest";
    public static readonly string TRANSFORM_VALUE = "I am a transformed value";

    public override Task<Message> WrapAsync(Message message, CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<Message>(cancellationToken);
        message.Header.Bag.Add(HEADER_KEY, TRANSFORM_VALUE);
        tcs.SetResult(message);
        return tcs.Task;
    }

    public override Task<Message> UnwrapAsync(Message message, CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<Message>(cancellationToken);
        var oldCommand = JsonSerializer.Deserialize<MyTransformableCommand>(message.Body.Value);
        oldCommand.Value = message.Header.Bag[HEADER_KEY].ToString();
        message.Body = new MessageBody(JsonSerializer.Serialize(oldCommand, new JsonSerializerOptions(JsonSerializerDefaults.General)));
        tcs.SetResult(message);
        return tcs.Task;
    }
}
