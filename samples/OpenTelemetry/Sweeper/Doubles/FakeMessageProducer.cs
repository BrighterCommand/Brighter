using Paramore.Brighter;

namespace Sweeper.Doubles;

public class FakeMessageProducer : IAmAMessageProducerSync
{
    public void Dispose()
    {
        //throw new NotImplementedException();
    }

    public int MaxOutStandingMessages { get; set; }
    public int MaxOutStandingCheckIntervalMilliSeconds { get; set; }

    public Dictionary<string, object> OutBoxBag { get; set; } = new Dictionary<string, object>();

    public void Send(Message message)
    {
        Console.WriteLine($"Message: {message.Body}");
    }

    public void SendWithDelay(Message message, int delayMilliseconds = 0)
    {
        Send(message);
    }
}
