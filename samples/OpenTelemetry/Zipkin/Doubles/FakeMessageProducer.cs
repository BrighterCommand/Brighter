using Paramore.Brighter;

namespace Zipkin.Doubles;

public class FakeMessageProducer : IAmAMessageProducer
{
    public void Dispose()
    {
        //throw new NotImplementedException();
    }

    public int MaxOutStandingMessages { get; set; }
    public int MaxOutStandingCheckIntervalMilliSeconds { get; set; }
}
