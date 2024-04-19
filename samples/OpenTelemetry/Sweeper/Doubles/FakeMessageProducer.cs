using Paramore.Brighter;

namespace Sweeper.Doubles;

public class FakeMessageProducer : IAmAMessageProducerSync
{
    public Publication Publication { get; } = new();
    
    public void Dispose()
    {
        //throw new NotImplementedException();
    }

    public void Send(Message message)
    {
        Console.WriteLine($"Message: {message.Body}");
    }

    public void SendWithDelay(Message message, int delayMilliseconds = 0)
    {
        Send(message);
    }
}
