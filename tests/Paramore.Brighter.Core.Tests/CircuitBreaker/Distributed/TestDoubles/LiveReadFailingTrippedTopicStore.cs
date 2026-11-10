using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.CircuitBreaker;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed.TestDoubles;

public sealed class LiveReadFailingTrippedTopicStore(IAmATrippedTopicStore inner) : IAmATrippedTopicStore
{
    public Task<TrippedTopicStoreRead> ReadAsync(DateTimeOffset now, TimeSpan tripLength, CancellationToken cancellationToken = default)
        => inner.ReadAsync(now, tripLength, cancellationToken);

    public Task<IReadOnlyList<TopicTrip>> EndExpiredAsync(DateTimeOffset now, TimeSpan tripLength, CancellationToken cancellationToken = default)
        => inner.EndExpiredAsync(now, tripLength, cancellationToken);

    public Task<IReadOnlyList<TopicTrip>> ReadLiveAsync(DateTimeOffset now, TimeSpan tripLength, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("The live trips cannot be read");

    public Task<IReadOnlyList<TripWriteResult>> WriteAsync(IReadOnlyList<TopicTrip> trips, CancellationToken cancellationToken = default)
        => inner.WriteAsync(trips, cancellationToken);
}
