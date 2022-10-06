using System.Threading.Tasks;

namespace Paramore.Brighter.Core.Tests.MessageSerilisation.Test_Doubles;

public class MySimpleTransformAsync : IAmAMessageTransformAsync
{
    public static readonly string HEADER_KEY = "MySimpleTransformTest"; 
    
    public Task<Message> Wrap(Message message)
    {
        var tcs = new TaskCompletionSource<Message>();
        message.Header.Bag.Add(HEADER_KEY, "I am a test value");
        tcs.SetResult(message);
        return tcs.Task;
    }

    public async Task<Message> Unwrap(Message message)
    {
        throw new System.NotImplementedException();
    }
}
