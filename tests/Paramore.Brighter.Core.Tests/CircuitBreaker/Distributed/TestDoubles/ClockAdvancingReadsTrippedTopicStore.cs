using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.CircuitBreaker;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed.TestDoubles;

public sealed class ClockAdvancingReadsTrippedTopicStore(IAmATrippedTopicStore inner, FakeTimeProvider clock, TimeSpan advancePerCall) : IAmATrippedTopicStore
{
    public Task<IReadOnlyList<TopicTrip>> EndExpiredAsync(DateTimeOffset now, TimeSpan tripLength, CancellationToken cancellationToken = default)
    {
        clock.Advance(advancePerCall);
        cancellationToken.ThrowIfCancellationRequested();
        return inner.EndExpiredAsync(now, tripLength, cancellationToken);
    }

    public Task<IReadOnlyList<TopicTrip>> ReadLiveAsync(DateTimeOffset now, TimeSpan tripLength, CancellationToken cancellationToken = default)
    {
        clock.Advance(advancePerCall);
        cancellationToken.ThrowIfCancellationRequested();
        return inner.ReadLiveAsync(now, tripLength, cancellationToken);
    }

    public Task<IReadOnlyList<TripWriteResult>> WriteAsync(IReadOnlyList<TopicTrip> trips, CancellationToken cancellationToken = default)
        => inner.WriteAsync(trips, cancellationToken);
}
