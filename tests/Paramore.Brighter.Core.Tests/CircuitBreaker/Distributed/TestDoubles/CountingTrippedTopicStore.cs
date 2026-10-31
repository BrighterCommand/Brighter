using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.CircuitBreaker;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed.TestDoubles;

public class CountingTrippedTopicStore : IAmATrippedTopicStore
{
    private readonly List<IReadOnlyList<TopicTrip>> _writes = new();

    public IReadOnlyList<IReadOnlyList<TopicTrip>> Writes
    {
        get
        {
            lock (_writes) return _writes.ToArray();
        }
    }

    public Task<TrippedTopicStoreRead> ReadAsync(DateTimeOffset now, TimeSpan tripLength, CancellationToken cancellationToken = default) =>
        Task.FromResult(new TrippedTopicStoreRead([], []));

    public Task<IReadOnlyList<TripWriteResult>> WriteAsync(IReadOnlyList<TopicTrip> trips, CancellationToken cancellationToken = default)
    {
        lock (_writes) _writes.Add(trips.ToArray());
        return Task.FromResult<IReadOnlyList<TripWriteResult>>([]);
    }
}
