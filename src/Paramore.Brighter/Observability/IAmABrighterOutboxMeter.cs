#region Licence
/* The MIT License (MIT)
Copyright © 2024 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

namespace Paramore.Brighter.Observability;

/// <summary>
/// Records Outbox metrics directly on OpenTelemetry instruments, independently of tracing and sampling.
/// </summary>
/// <remarks>
/// Contract:
/// <list type="bullet">
///   <item><paramref name="destination"/> may be <see cref="RoutingKey.Empty"/>.</item>
///   <item>A negative <c>wait</c> passed to <see cref="RecordPublishDuration"/> is recorded as zero.</item>
///   <item>No member may throw — implementations must absorb faults internally.</item>
/// </list>
/// </remarks>
public interface IAmABrighterOutboxMeter
{
    /// <summary>Record one message successfully written to the Outbox.</summary>
    /// <param name="destination">The message's routing key / destination topic.</param>
    void AddMessageAdded(RoutingKey destination);

    /// <summary>Record one message recorded as dispatched from the Outbox.</summary>
    /// <param name="destination">The message's routing key / destination topic.</param>
    /// <param name="clearSource">What initiated the clear operation.</param>
    void AddMessageCleared(RoutingKey destination, OutboxClearSource clearSource);

    /// <summary>
    /// Record how long a message waited between being created for the Outbox and being recorded as dispatched.
    /// </summary>
    /// <param name="wait">
    /// The duration between <c>Header.TimeStamp</c> (message creation) and the <c>MarkDispatched</c> instant.
    /// This value <b>includes message mapping/transform time</b> and is <b>subject to clock skew</b>;
    /// it is not a pure store-to-broker duration. Negative values are recorded as zero.
    /// </param>
    /// <param name="destination">The message's routing key / destination topic.</param>
    /// <param name="clearSource">What initiated the clear operation.</param>
    void RecordPublishDuration(TimeSpan wait, RoutingKey destination, OutboxClearSource clearSource);

    /// <summary>
    /// Returns <c>false</c> when no <see cref="System.Diagnostics.Metrics.MeterListener"/> is attached
    /// to any of the three Outbox instruments, allowing callers to skip all tag-building work at zero cost.
    /// </summary>
    bool Enabled { get; }
}
