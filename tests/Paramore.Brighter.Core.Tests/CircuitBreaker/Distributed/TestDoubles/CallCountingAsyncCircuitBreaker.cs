#region Licence

/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */
#endregion

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.CircuitBreaker;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed.TestDoubles;

public class CallCountingAsyncCircuitBreaker(DistributedOutboxCircuitBreaker inner) : IAmAnOutboxCircuitBreaker, IAmAnOutboxCircuitBreakerAsync
{
    private int _coolDowns;
    private int _directTrips;
    private int _batchesBegun;
    private int _asyncReads;
    private int _syncListings;

    public int CoolDowns => Volatile.Read(ref _coolDowns);
    public int DirectTrips => Volatile.Read(ref _directTrips);
    public int BatchesBegun => Volatile.Read(ref _batchesBegun);
    public int AsyncReads => Volatile.Read(ref _asyncReads);
    public int SyncListings => Volatile.Read(ref _syncListings);

    public void CoolDown()
    {
        Interlocked.Increment(ref _coolDowns);
        inner.CoolDown();
    }

    public void TripTopic(RoutingKey topic)
    {
        Interlocked.Increment(ref _directTrips);
        inner.TripTopic(topic);
    }

    public IEnumerable<RoutingKey> TrippedTopics
    {
        get
        {
            Interlocked.Increment(ref _syncListings);
            return inner.TrippedTopics;
        }
    }

    public IAmATripBatch BeginTripBatch()
    {
        Interlocked.Increment(ref _batchesBegun);
        return inner.BeginTripBatch();
    }

    public Task<IReadOnlyCollection<RoutingKey>> ReadTrippedTopicsAsync(CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _asyncReads);
        return inner.ReadTrippedTopicsAsync(cancellationToken);
    }
}
