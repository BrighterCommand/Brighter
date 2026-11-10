using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.CircuitBreaker;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed.TestDoubles;

public class CountingTrippedTopicStore : IAmATrippedTopicStore
{
    private readonly WriteRecorder _recorder = new();

    public IReadOnlyList<IReadOnlyList<TopicTrip>> Writes => _recorder.Writes;

    private int _reads;

    public int Reads => Volatile.Read(ref _reads);

    public Task<IReadOnlyList<TopicTrip>> EndExpiredAsync(DateTimeOffset now, TimeSpan tripLength, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _reads);
        return Task.FromResult<IReadOnlyList<TopicTrip>>([]);
    }

    public Task<IReadOnlyList<TopicTrip>> ReadLiveAsync(DateTimeOffset now, TimeSpan tripLength, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<TopicTrip>>([]);

    public Task<IReadOnlyList<TripWriteResult>> WriteAsync(IReadOnlyList<TopicTrip> trips, CancellationToken cancellationToken = default)
    {
        _recorder.Record(trips);
        return Task.FromResult<IReadOnlyList<TripWriteResult>>([]);
    }
}
