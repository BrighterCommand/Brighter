#region Licence

/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway;

/// <summary>
/// Wraps an <see cref="IAmAChannelAsync"/> and re-polls <see cref="ReceiveAsync"/> when the broker
/// returns <see cref="MessageType.MT_NONE"/> — Kafka on CI can be slow to deliver a message that
/// is coming, so a spurious early MT_NONE should not fail a positive assertion.
///
/// The retry is bounded to the caller's requested timeout: ReceiveAsync(t) never waits longer than t
/// in total. This preserves the conformance contract that a receive is a single bounded receive — the
/// FR-2 / FR-9 before-D negative arm and FR-15's "redelivered within 5 s" assertion both depend on a
/// receive respecting its timeout, so re-polling must never extend the window past the delay under test.
/// </summary>
public class RetryableChannelAsync(IAmAChannelAsync inner) : IAmAChannelAsync
{
    public ChannelName Name => inner.Name;

    public RoutingKey RoutingKey => inner.RoutingKey;

    public Task AcknowledgeAsync(Message message, CancellationToken cancellationToken = default) =>
        inner.AcknowledgeAsync(message, cancellationToken);

    public Task PurgeAsync(CancellationToken cancellationToken = default) =>
        inner.PurgeAsync(cancellationToken);

    public async Task<Message> ReceiveAsync(TimeSpan? timeout, CancellationToken cancellationToken = default)
    {
        if (timeout is null)
            return await inner.ReceiveAsync(timeout, cancellationToken);

        var stopwatch = Stopwatch.StartNew();
        var message = await inner.ReceiveAsync(timeout, cancellationToken);
        while (message.Header.MessageType == MessageType.MT_NONE)
        {
            var remaining = timeout.Value - stopwatch.Elapsed;
            if (remaining <= TimeSpan.Zero)
                break;

            message = await inner.ReceiveAsync(remaining, cancellationToken);
        }

        return message;
    }

    public Task<bool> RejectAsync(Message message, MessageRejectionReason? reason = null,
        CancellationToken cancellationToken = default) =>
        inner.RejectAsync(message, reason, cancellationToken);

    public Task NackAsync(Message message, CancellationToken cancellationToken = default) =>
        inner.NackAsync(message, cancellationToken);

    public Task<bool> RequeueAsync(Message message, TimeSpan? timeOut = null,
        CancellationToken cancellationToken = default) =>
        inner.RequeueAsync(message, timeOut, cancellationToken);

    public void Enqueue(params Message[] message) => inner.Enqueue(message);

    public void Stop(RoutingKey topic) => inner.Stop(topic);

    public void Dispose() => inner.Dispose();

    public ValueTask DisposeAsync() =>
        inner is IAsyncDisposable asyncDisposable
            ? asyncDisposable.DisposeAsync()
            : ValueTask.CompletedTask;
}
