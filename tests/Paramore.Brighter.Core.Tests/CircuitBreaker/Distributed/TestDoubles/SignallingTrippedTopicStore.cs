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

public enum WriteHold
{
    None,
    AllWrites,
    FirstWriteOnly
}

public class SignallingTrippedTopicStore(InMemoryTrippedTopicStore? inner = null, WriteHold hold = WriteHold.None) : IAmATrippedTopicStore
{
    private readonly TaskCompletionSource<bool> _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> _writeStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> _secondWriteStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> _writeCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly WriteRecorder _recorder = new();
    private int _readCount;

    public int ReadCount => Volatile.Read(ref _readCount);

    public int? WriteThreadId { get; private set; }

    public IReadOnlyList<TopicTrip> WrittenTrips => _recorder.Trips;

    public Task WriteStarted => _writeStarted.Task;

    public Task SecondWriteStarted => _secondWriteStarted.Task;

    public Task WriteCompleted => _writeCompleted.Task;

    public void ReleaseWrites() => _release.TrySetResult(true);

    public Task<IReadOnlyList<TopicTrip>> EndExpiredAsync(DateTimeOffset now, TimeSpan tripLength, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _readCount);
        return inner is null
            ? Task.FromResult<IReadOnlyList<TopicTrip>>([])
            : inner.EndExpiredAsync(now, tripLength, cancellationToken);
    }

    public Task<IReadOnlyList<TopicTrip>> ReadLiveAsync(DateTimeOffset now, TimeSpan tripLength, CancellationToken cancellationToken = default)
        => inner is null
            ? Task.FromResult<IReadOnlyList<TopicTrip>>([])
            : inner.ReadLiveAsync(now, tripLength, cancellationToken);

    public async Task<IReadOnlyList<TripWriteResult>> WriteAsync(IReadOnlyList<TopicTrip> trips, CancellationToken cancellationToken = default)
    {
        WriteThreadId = Environment.CurrentManagedThreadId;
        var writeNumber = _recorder.Record(trips);
        _writeStarted.TrySetResult(true);
        if (writeNumber > 1)
            _secondWriteStarted.TrySetResult(true);

        if (IsHeld(writeNumber))
            await _release.Task;

        IReadOnlyList<TripWriteResult> results = [];
        if (inner is not null)
            results = await inner.WriteAsync(trips, cancellationToken);

        _writeCompleted.TrySetResult(true);
        return results;
    }

    private bool IsHeld(int writeNumber) => hold == WriteHold.AllWrites || (hold == WriteHold.FirstWriteOnly && writeNumber == 1);
}
