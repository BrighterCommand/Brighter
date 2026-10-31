using Paramore.Brighter.Base.Test.CircuitBreaker;
using Paramore.Brighter.CircuitBreaker;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed;

public class InMemoryTrippedTopicStoreWriteContractTests : TrippedTopicStoreWriteContractTest
{
    protected override IAmATrippedTopicStore Store { get; } = new InMemoryTrippedTopicStore();
}
