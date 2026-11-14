using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.CircuitBreaker;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed.TestDoubles;

public sealed class LiveReadFailingTrippedTopicStore(IAmATrippedTopicStore inner) : ForwardingTrippedTopicStore(inner)
{
    public override Task<IReadOnlyList<TopicTrip>> ReadLiveAsync(TripWindow window, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("The live trips cannot be read");
}
