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

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway;

/// <summary>
/// Wraps an <see cref="IAmAChannelSync"/> and re-polls <see cref="Receive"/> when the broker
/// returns <see cref="MessageType.MT_NONE"/> — Kafka on CI can be slow to deliver a message that
/// is coming, so a spurious early MT_NONE should not fail a positive assertion.
///
/// It re-polls on a <see cref="ChannelFailureException"/> for the same reason: a topic created
/// moments earlier may not have propagated across the cluster, so the first consume can fail for a
/// topic that is on its way. The pump rides that out (it catches ChannelFailureException, waits and
/// continues); a test calling Receive directly has no pump, so without this it would be stricter than
/// production. A failure that outlasts the budget is rethrown, so a genuine outage is still reported
/// as the exception rather than masked as an empty receive.
///
/// The retry is bounded to the caller's requested timeout: Receive(t) never waits longer than t in
/// total. This preserves the conformance contract that Receive(t) is a single bounded receive — the
/// FR-2 / FR-9 before-D negative arm and FR-15's "redelivered within 5 s" assertion both depend on a
/// receive respecting its timeout, so re-polling must never extend the window past the delay under test.
/// For the same reason there is no pause before or between attempts: every retry draws from what is
/// left of the caller's budget.
/// </summary>
public class RetryableChannelSync(IAmAChannelSync inner) : IAmAChannelSync
{
    public ChannelName Name => inner.Name;

    public RoutingKey RoutingKey => inner.RoutingKey;

    public void Acknowledge(Message message) => inner.Acknowledge(message);

    public void Purge() => inner.Purge();

    public Message Receive(TimeSpan? timeout)
    {
        if (timeout is null)
            return inner.Receive(timeout);

        var budget = timeout.Value;
        var stopwatch = Stopwatch.StartNew();
        var remaining = budget;

        while (true)
        {
            Message message;
            try
            {
                message = inner.Receive(remaining);
            }
            catch (ChannelFailureException)
            {
                remaining = budget - stopwatch.Elapsed;
                if (remaining <= TimeSpan.Zero)
                    throw;

                continue;
            }

            remaining = budget - stopwatch.Elapsed;
            if (message.Header.MessageType != MessageType.MT_NONE || remaining <= TimeSpan.Zero)
                return message;
        }
    }

    public bool Reject(Message message, MessageRejectionReason? reason = null) =>
        inner.Reject(message, reason);

    public void Nack(Message message) => inner.Nack(message);

    public bool Requeue(Message message, TimeSpan? timeOut = null) =>
        inner.Requeue(message, timeOut);

    public void Enqueue(params Message[] message) => inner.Enqueue(message);

    public void Stop(RoutingKey topic) => inner.Stop(topic);

    public void Dispose() => inner.Dispose();
}
