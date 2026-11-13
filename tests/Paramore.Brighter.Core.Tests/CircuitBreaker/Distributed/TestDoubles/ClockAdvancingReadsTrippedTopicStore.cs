using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.CircuitBreaker;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed.TestDoubles;

public sealed class ClockAdvancingReadsTrippedTopicStore(IAmATrippedTopicStore inner, FakeTimeProvider clock, TimeSpan advancePerCall) : ForwardingTrippedTopicStore(inner)
{
    public override Task<IReadOnlyList<TopicTrip>> EndExpiredAsync(DateTimeOffset now, TimeSpan tripLength, CancellationToken cancellationToken = default)
    {
        clock.Advance(advancePerCall);
        cancellationToken.ThrowIfCancellationRequested();
        return base.EndExpiredAsync(now, tripLength, cancellationToken);
    }

    public override Task<IReadOnlyList<TopicTrip>> ReadLiveAsync(DateTimeOffset now, TimeSpan tripLength, CancellationToken cancellationToken = default)
    {
        clock.Advance(advancePerCall);
        cancellationToken.ThrowIfCancellationRequested();
        return base.ReadLiveAsync(now, tripLength, cancellationToken);
    }
}
