#region Licence

/* The MIT License (MIT)
Copyright © 2026 gabisonia <irakli.gabisonia94@gmail.com>

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

/// <summary>
/// An empty transport that records receiving and disposal, optionally failing during cleanup.
/// </summary>
internal sealed class InMemoryUnopenedConsumer(bool throwOnDispose)
    : IAmAMessageConsumerSync, IAmAMessageConsumerAsync
{
    private int _disposeCount;
    private int _receiveCount;

    public int DisposeCount => Volatile.Read(ref _disposeCount);
    public int ReceiveCount => Volatile.Read(ref _receiveCount);

    public Message[] Receive(TimeSpan? timeOut = null)
    {
        Interlocked.Increment(ref _receiveCount);
        return [];
    }

    public Task<Message[]> ReceiveAsync(TimeSpan? timeOut = null, CancellationToken cancellationToken = default)
        => Task.FromResult(Receive(timeOut));

    public void Acknowledge(Message message) { }
    public void Nack(Message message) { }
    public bool Reject(Message message, MessageRejectionReason? reason = null) => true;
    public bool Requeue(Message message, TimeSpan? delay = null) => true;
    public void Purge() { }
    public Task AcknowledgeAsync(Message message, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task NackAsync(Message message, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<bool> RejectAsync(Message message, MessageRejectionReason? reason = null, CancellationToken cancellationToken = default)
        => Task.FromResult(true);
    public Task<bool> RequeueAsync(Message message, TimeSpan? delay = null, CancellationToken cancellationToken = default)
        => Task.FromResult(true);
    public Task PurgeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public void Dispose()
    {
        Interlocked.Increment(ref _disposeCount);
        if (throwOnDispose)
            throw new InvalidOperationException("Unopened transport disposal failed.");
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return default;
    }
}
