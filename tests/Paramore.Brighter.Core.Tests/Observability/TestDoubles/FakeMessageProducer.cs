using System.Collections.Generic;

namespace Paramore.Brighter.Core.Tests.Observability.TestDoubles;

public class FakeMessageProducer : IAmAMessageProducer
{
    public Publication Publication { get; } = new();
    
    public void Dispose()
    {
        //Left Blank
    }
}
