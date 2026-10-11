#region Licence

/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;

internal sealed class InMemoryDisposalFailureConsumer(InMemoryMessageConsumer inner, Exception? failure, bool failAcknowledgment)
    : IAmAMessageConsumerSync, IAmAMessageConsumerAsync
{
    private int _disposeCount;
    private readonly TaskCompletionSource<bool> _disposalAttempted = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int DisposeCount => Volatile.Read(ref _disposeCount);

    public Task DisposalAttempted => _disposalAttempted.Task;

    public Message[] Receive(TimeSpan? timeOut = null) => inner.Receive(timeOut);

    public Task<Message[]> ReceiveAsync(TimeSpan? timeOut = null, CancellationToken cancellationToken = default)
        => inner.ReceiveAsync(timeOut, cancellationToken);

    public void Acknowledge(Message message)
    {
        // A failed acknowledgment lets the pump exit before it disposes the channel.
        if (failure is not null && failAcknowledgment)
            throw new InvalidOperationException("Acknowledgment failed");

        inner.Acknowledge(message);
    }

    public Task AcknowledgeAsync(Message message, CancellationToken cancellationToken = default)
    {
        Acknowledge(message);
        return Task.CompletedTask;
    }

    public bool Reject(Message message, MessageRejectionReason? reason = null) => inner.Reject(message, reason);

    public Task<bool> RejectAsync(Message message, MessageRejectionReason? reason = null, CancellationToken cancellationToken = default)
        => inner.RejectAsync(message, reason, cancellationToken);

    public void Nack(Message message) => inner.Nack(message);

    public Task NackAsync(Message message, CancellationToken cancellationToken = default)
        => inner.NackAsync(message, cancellationToken);

    public bool Requeue(Message message, TimeSpan? delay = null) => inner.Requeue(message, delay);

    public Task<bool> RequeueAsync(Message message, TimeSpan? delay = null, CancellationToken cancellationToken = default)
        => inner.RequeueAsync(message, delay, cancellationToken);

    public void Purge() => inner.Purge();

    public Task PurgeAsync(CancellationToken cancellationToken = default) => inner.PurgeAsync(cancellationToken);

    public void Dispose()
    {
        Interlocked.Increment(ref _disposeCount);
        inner.Dispose();
        _disposalAttempted.TrySetResult(true);
        if (failure is not null)
            throw failure;
    }

    public async ValueTask DisposeAsync()
    {
        Interlocked.Increment(ref _disposeCount);
        await inner.DisposeAsync();
        _disposalAttempted.TrySetResult(true);
        if (failure is not null)
            throw failure;
    }

    public void ReleaseResources() => inner.Dispose();
}
