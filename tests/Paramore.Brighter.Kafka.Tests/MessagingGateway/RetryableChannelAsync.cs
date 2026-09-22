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
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway;

/// <summary>
/// Wraps an <see cref="IAmAChannelAsync"/> and re-polls <see cref="ReceiveAsync"/> when the broker
/// returns <see cref="MessageType.MT_NONE"/> — Kafka on CI can be slow to deliver a message that
/// is coming, so a spurious early MT_NONE should not fail a positive assertion.
///
/// It re-polls on a <see cref="ChannelFailureException"/> for the same reason: a topic created
/// moments earlier may not have propagated across the cluster, so the first consume can fail for a
/// topic that is on its way. The pump rides that out (it catches ChannelFailureException, waits and
/// continues); a test calling ReceiveAsync directly has no pump, so without this it would be stricter
/// than production. ⛔ The failure is only swallowed when a message actually arrives: if the budget expires
/// having received nothing, the last ChannelFailureException is rethrown, because a consumer polling a
/// topic that really is missing returns MT_NONE on its later polls rather than failing again — so
/// without this a genuine outage would be masked as an empty receive.
///
/// The retry is bounded to the caller's requested timeout: ReceiveAsync(t) never waits longer than t
/// in total. This preserves the conformance contract that a receive is a single bounded receive — the
/// FR-2 / FR-9 before-D negative arm and FR-15's "redelivered within 5 s" assertion both depend on a
/// receive respecting its timeout, so re-polling must never extend the window past the delay under test.
/// For the same reason there is no pause before or between attempts: every retry draws from what is
/// left of the caller's budget.
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

        var budget = timeout.Value;
        var stopwatch = Stopwatch.StartNew();
        var remaining = budget;
        ExceptionDispatchInfo? failure = null;

        while (true)
        {
            Message message;
            try
            {
                message = await inner.ReceiveAsync(remaining, cancellationToken);
            }
            catch (ChannelFailureException channelFailure)
            {
                failure = ExceptionDispatchInfo.Capture(channelFailure);
                remaining = budget - stopwatch.Elapsed;
                if (remaining <= TimeSpan.Zero)
                    throw;

                continue;
            }

            if (message.Header.MessageType != MessageType.MT_NONE)
                return message;

            remaining = budget - stopwatch.Elapsed;
            if (remaining > TimeSpan.Zero)
                continue;

            failure?.Throw();
            return message;
        }
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
