#region License

/* The MIT License (MIT)
Copyright © 2025 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.CircuitBreaker;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed.TestDoubles;

public class AbandonedCallCountingTrippedTopicStore : IAmATrippedTopicStore
{
    private int _readCount;
    private int _writeCount;

    public int ReadCount => Volatile.Read(ref _readCount);

    public int WriteCount => Volatile.Read(ref _writeCount);

    public Task<IReadOnlyList<TopicTrip>> EndExpiredAsync(TripWindow window, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _readCount);
        return new TaskCompletionSource<IReadOnlyList<TopicTrip>>().Task;
    }

    public Task<IReadOnlyList<TopicTrip>> ReadLiveAsync(TripWindow window, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<TopicTrip>>([]);

    public Task<IReadOnlyList<TripWriteResult>> WriteAsync(IReadOnlyList<TopicTrip> trips, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _writeCount);
        return new TaskCompletionSource<IReadOnlyList<TripWriteResult>>().Task;
    }
}
