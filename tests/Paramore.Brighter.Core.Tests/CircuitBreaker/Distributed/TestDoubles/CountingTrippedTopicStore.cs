using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.CircuitBreaker;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed.TestDoubles;

public class CountingTrippedTopicStore : EmptyTrippedTopicStore
{
    private readonly WriteRecorder _recorder = new();

    public IReadOnlyList<IReadOnlyList<TopicTrip>> Writes => _recorder.Writes;

    private int _reads;

    public int Reads => Volatile.Read(ref _reads);

    public override Task<IReadOnlyList<TopicTrip>> EndExpiredAsync(TripWindow window, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _reads);
        return base.EndExpiredAsync(window, cancellationToken);
    }

    public override Task<IReadOnlyList<TripWriteResult>> WriteAsync(IReadOnlyList<TopicTrip> trips, CancellationToken cancellationToken = default)
    {
        _recorder.Record(trips);
        return base.WriteAsync(trips, cancellationToken);
    }
}
