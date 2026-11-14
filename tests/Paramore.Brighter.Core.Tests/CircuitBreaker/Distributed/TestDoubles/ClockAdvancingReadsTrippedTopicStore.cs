using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.CircuitBreaker;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed.TestDoubles;

public sealed class ClockAdvancingReadsTrippedTopicStore(IAmATrippedTopicStore inner, FakeTimeProvider clock, TimeSpan advancePerCall) : ForwardingTrippedTopicStore(inner)
{
    public override Task<IReadOnlyList<TopicTrip>> EndExpiredAsync(TripWindow window, CancellationToken cancellationToken = default)
    {
        clock.Advance(advancePerCall);
        cancellationToken.ThrowIfCancellationRequested();
        return base.EndExpiredAsync(window, cancellationToken);
    }

    public override Task<IReadOnlyList<TopicTrip>> ReadLiveAsync(TripWindow window, CancellationToken cancellationToken = default)
    {
        clock.Advance(advancePerCall);
        cancellationToken.ThrowIfCancellationRequested();
        return base.ReadLiveAsync(window, cancellationToken);
    }
}
