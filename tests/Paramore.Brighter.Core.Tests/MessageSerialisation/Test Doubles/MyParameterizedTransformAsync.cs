using System.Text.Json;
using System.Threading.Tasks;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;

public class MyParameterizedTransformAsync : IAmAMessageTransformAsync
{
    public static readonly string HEADER_KEY = "MyParameterizedTransformTest";
    private string _template;
    private string _displayFormat;


    public void InitializeWrapFromAttributeParams(params object[] initializerList)
    {
        _template = (string)initializerList[0];
    }

    public void InitializeUnwrapFromAttributeParams(params object[] initializerList)
    {
        _displayFormat = (string)initializerList[0];
    }

    public Task<Message> Wrap(Message message)
    {
        var tcs = new TaskCompletionSource<Message>();
        message.Header.Bag.Add(HEADER_KEY, _template);
        tcs.SetResult(message);
        return tcs.Task;
    }

    public Task<Message> Unwrap(Message message)
    {
        var tcs = new TaskCompletionSource<Message>();
        var oldCommand = JsonSerializer.Deserialize<MyTransformableCommand>(message.Body.Value);
        oldCommand.Value = string.Format(_displayFormat, oldCommand.Value);
        message.Body = new MessageBody(JsonSerializer.Serialize(oldCommand, new JsonSerializerOptions(JsonSerializerDefaults.General)));
        tcs.SetResult(message);
        return tcs.Task;
    }

    public void Dispose() { }

}
