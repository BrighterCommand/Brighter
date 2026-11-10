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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.CircuitBreaker;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed.TestDoubles;

public class IndividuallyHeldReadsTrippedTopicStore(IAmATrippedTopicStore inner) : IAmATrippedTopicStore
{
    private readonly ConcurrentDictionary<int, ReadSlot> _slots = new();
    private readonly AsyncLocal<ReadSlot?> _slotOfThisRead = new();
    private int _readCount;

    public Task ReadStarted(int readNumber) => SlotFor(readNumber).Started.Task;

    public void CompleteRead(int readNumber, TrippedTopicStoreRead read) => SlotFor(readNumber).Result.TrySetResult(read);

    public void FailRead(int readNumber, Exception failure) => SlotFor(readNumber).Result.TrySetException(failure);

    public Task<TrippedTopicStoreRead> ReadAsync(DateTimeOffset now, TimeSpan tripLength, CancellationToken cancellationToken = default)
    {
        var slot = SlotFor(Interlocked.Increment(ref _readCount));
        slot.Started.TrySetResult(true);
        return slot.Result.Task;
    }

    public Task<IReadOnlyList<TopicTrip>> EndExpiredAsync(DateTimeOffset now, TimeSpan tripLength, CancellationToken cancellationToken = default)
    {
        var slot = SlotFor(Interlocked.Increment(ref _readCount));
        _slotOfThisRead.Value = slot;
        slot.Started.TrySetResult(true);
        return EndedOfAsync(slot);
    }

    public async Task<IReadOnlyList<TopicTrip>> ReadLiveAsync(DateTimeOffset now, TimeSpan tripLength, CancellationToken cancellationToken = default)
        => (await _slotOfThisRead.Value!.Result.Task.ConfigureAwait(false)).Live;

    private static async Task<IReadOnlyList<TopicTrip>> EndedOfAsync(ReadSlot slot)
        => (await slot.Result.Task.ConfigureAwait(false)).Ended;

    public Task<IReadOnlyList<TripWriteResult>> WriteAsync(IReadOnlyList<TopicTrip> trips, CancellationToken cancellationToken = default)
        => inner.WriteAsync(trips, cancellationToken);

    private ReadSlot SlotFor(int readNumber) => _slots.GetOrAdd(readNumber, _ => new ReadSlot());

    private sealed class ReadSlot
    {
        public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<TrippedTopicStoreRead> Result { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
