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
using Polly.CircuitBreaker;

namespace Paramore.Brighter.Extensions.Tests.TestDoubles;

internal sealed class InMemoryReceiveHealthConsumer(RoutingKey routingKey, InternalBus bus, int failures, bool brokenCircuit = false)
    : IAmAMessageConsumerSync, IAmAMessageConsumerAsync
{
    private readonly InMemoryMessageConsumer _inner = new(routingKey, bus, TimeProvider.System);
    private readonly TaskCompletionSource<bool> _failuresObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> _recovered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> _recoveryObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _attempts;

    public Task FailuresObserved => _failuresObserved.Task;

    public Task RecoveryObserved => _recoveryObserved.Task;

    public void Recover() => _recovered.TrySetResult(true);

    Message[] IAmAMessageConsumerSync.Receive(TimeSpan? timeOut)
    {
        FailOrWaitForRecoveryAsync(CancellationToken.None).GetAwaiter().GetResult();
        return _inner.Receive(timeOut);
    }

    async Task<Message[]> IAmAMessageConsumerAsync.ReceiveAsync(TimeSpan? timeOut, CancellationToken cancellationToken)
    {
        await FailOrWaitForRecoveryAsync(cancellationToken).ConfigureAwait(false);
        return await _inner.ReceiveAsync(timeOut, cancellationToken).ConfigureAwait(false);
    }

    public void Acknowledge(Message message) => _inner.Acknowledge(message);

    public void Nack(Message message) => _inner.Nack(message);

    public bool Reject(Message message, MessageRejectionReason? reason = null) => _inner.Reject(message, reason);

    public bool Requeue(Message message, TimeSpan? delay = null) => _inner.Requeue(message, delay);

    public void Purge() => _inner.Purge();

    public void Dispose() => _inner.Dispose();

    public Task AcknowledgeAsync(Message message, CancellationToken cancellationToken = default)
        => _inner.AcknowledgeAsync(message, cancellationToken);

    public Task NackAsync(Message message, CancellationToken cancellationToken = default)
        => _inner.NackAsync(message, cancellationToken);

    public Task<bool> RejectAsync(Message message, MessageRejectionReason? reason = null, CancellationToken cancellationToken = default)
        => _inner.RejectAsync(message, reason, cancellationToken);

    public Task<bool> RequeueAsync(Message message, TimeSpan? delay = null, CancellationToken cancellationToken = default)
        => _inner.RequeueAsync(message, delay, cancellationToken);

    public Task PurgeAsync(CancellationToken cancellationToken = default) => _inner.PurgeAsync(cancellationToken);

    public ValueTask DisposeAsync() => _inner.DisposeAsync();

    private async Task FailOrWaitForRecoveryAsync(CancellationToken cancellationToken)
    {
        var attempt = Interlocked.Increment(ref _attempts);
        if (attempt <= failures)
        {
            if (brokenCircuit)
                throw new ChannelFailureException("The in-memory broker circuit is open.", new BrokenCircuitException());

            throw new ChannelFailureException("The in-memory broker is unavailable.");
        }

        if (attempt > failures + 1)
            _recoveryObserved.TrySetResult(true);

        // Reaching the next receive guarantees the pump has caught every preceding failure.
        _failuresObserved.TrySetResult(true);
        await _recovered.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }
}
