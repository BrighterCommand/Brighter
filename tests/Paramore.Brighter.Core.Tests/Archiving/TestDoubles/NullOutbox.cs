using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Core.Tests.Archiving.TestDoubles;

/// <summary>
/// An outbox that implements only the base IAmAnOutbox marker interface.
/// Neither IAmAnOutboxSync nor IAmAnOutboxAsync — used to test the "no outbox" guard path.
/// </summary>
public class NullOutbox : IAmAnOutbox
{
    public IAmABrighterTracer? Tracer { set { } }
}
