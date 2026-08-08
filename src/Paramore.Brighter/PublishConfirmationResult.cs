#region Licence
/* The MIT License (MIT)
Copyright © 2025 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using System.Diagnostics;

namespace Paramore.Brighter
{
    /// <summary>
    /// Carries the outcome of a broker publish confirmation back to the <see cref="OutboxProducerMediator"/> via
    /// <see cref="ISupportPublishConfirmation.OnMessagePublished"/>. A producer that supports confirmation returns from
    /// its send call before the broker has acknowledged the message, then later raises the event with this result once
    /// the broker confirms (or fails to confirm) persistence. The mediator uses it to decide whether to mark the
    /// message dispatched in the Outbox and to enrich the confirmation observability span.
    /// </summary>
    /// <param name="Success">
    /// <c>true</c> when the broker confirmed the message was persisted; <c>false</c> when the broker reported a failure
    /// to persist. A failed confirmation leaves the message un-dispatched so the Outbox sweeper can retry it.
    /// </param>
    /// <param name="MessageId">
    /// The id of the message the broker confirmed (or failed to confirm). <see cref="Id.Empty"/> indicates the producer
    /// could not determine the id from the broker response.
    /// </param>
    /// <param name="Topic">
    /// The wire topic (<see cref="MessageHeader.Topic"/>) the message was published to, used to trip the circuit breaker
    /// on the exact address that failed. <c>null</c> when the producer could not supply it.
    /// </param>
    /// <param name="PublishSpanContext">
    /// The <see cref="ActivityContext"/> of the publish span captured at send time, so the confirmation span can be
    /// linked back to the original publish. <c>null</c> when no publish activity was active at send time.
    /// </param>
    public sealed record PublishConfirmationResult(
        bool Success,
        Id MessageId,
        RoutingKey? Topic,
        ActivityContext? PublishSpanContext);
}
