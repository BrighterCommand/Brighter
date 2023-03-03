using System.Collections.Generic;

namespace Paramore.Brighter.Core.Tests.Observability.TestDoubles;

public class FakeMessageProducer : IAmAMessageProducer
{
    public void Dispose()
    {
        //Left Blank
    }

    public int MaxOutStandingMessages { get; set; }
    public int MaxOutStandingCheckIntervalMilliSeconds { get; set; }
    
    public Dictionary<string, object> OutBoxBag { get; set; }
}
