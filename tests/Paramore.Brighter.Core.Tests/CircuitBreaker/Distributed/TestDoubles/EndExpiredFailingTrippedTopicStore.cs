using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.CircuitBreaker;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed.TestDoubles;

public sealed class EndExpiredFailingTrippedTopicStore(IAmATrippedTopicStore inner) : IAmATrippedTopicStore
{
    private int _liveReadCount;

    public int LiveReadCount => Volatile.Read(ref _liveReadCount);

    public Task<TrippedTopicStoreRead> ReadAsync(DateTimeOffset now, TimeSpan tripLength, CancellationToken cancellationToken = default)
        => inner.ReadAsync(now, tripLength, cancellationToken);

    public Task<IReadOnlyList<TopicTrip>> EndExpiredAsync(DateTimeOffset now, TimeSpan tripLength, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("The expired trips cannot be ended");

    public Task<IReadOnlyList<TopicTrip>> ReadLiveAsync(DateTimeOffset now, TimeSpan tripLength, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _liveReadCount);
        return inner.ReadLiveAsync(now, tripLength, cancellationToken);
    }

    public Task<IReadOnlyList<TripWriteResult>> WriteAsync(IReadOnlyList<TopicTrip> trips, CancellationToken cancellationToken = default)
        => inner.WriteAsync(trips, cancellationToken);
}
