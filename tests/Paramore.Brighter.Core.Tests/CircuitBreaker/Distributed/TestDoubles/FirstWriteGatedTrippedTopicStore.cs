#region License

/* The MIT License (MIT)
Copyright © 2025 Michael Freeman <mike.c.freeman@gmail.com>

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

# endregion

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.CircuitBreaker;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed.TestDoubles;

public class FirstWriteGatedTrippedTopicStore : IAmATrippedTopicStore
{
    private readonly TaskCompletionSource<bool> _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> _firstWriteStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> _secondWriteStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<TopicTrip> _writtenTrips = new();

    public Task FirstWriteStarted => _firstWriteStarted.Task;

    public Task SecondWriteStarted => _secondWriteStarted.Task;

    public IReadOnlyList<TopicTrip> WrittenTrips
    {
        get
        {
            lock (_writtenTrips) return _writtenTrips.ToArray();
        }
    }

    public void ReleaseFirstWrite() => _release.TrySetResult(true);

    public Task<TrippedTopicStoreRead> ReadAsync(DateTimeOffset now, TimeSpan tripLength, CancellationToken cancellationToken = default) =>
        Task.FromResult(new TrippedTopicStoreRead([], []));

    public async Task<IReadOnlyList<TripWriteResult>> WriteAsync(IReadOnlyList<TopicTrip> trips, CancellationToken cancellationToken = default)
    {
        int writeNumber;
        lock (_writtenTrips)
        {
            _writtenTrips.AddRange(trips);
            writeNumber = _writtenTrips.Count;
        }

        if (writeNumber == 1)
        {
            _firstWriteStarted.TrySetResult(true);
            await _release.Task;
        }
        else
        {
            _secondWriteStarted.TrySetResult(true);
        }

        return [];
    }
}
