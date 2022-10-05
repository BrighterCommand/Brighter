namespace Paramore.Brighter.Core.Tests.MessageSerilisation.Test_Doubles;

public class MySimpleTransform : IAmAMessageTransform
{
    public static readonly string HEADER_KEY = "MySimpleTransformTest"; 
    
    public Message Wrap(Message message)
    {
       message.Header.Bag.Add(HEADER_KEY, "I am a test value");
       return message;
    }

    public Message Unwrap(Message message)
    {
        throw new System.NotImplementedException();
    }
}
