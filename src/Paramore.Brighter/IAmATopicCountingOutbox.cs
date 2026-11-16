#region Licence
/* The MIT License (MIT)
Copyright © 2026 Guilherme Appel

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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter
{
    /// <summary>
    /// Role: An outbox that can count its stale messages per topic.
    /// Responsibility: Doing the count of undispatched messages for each requested topic, each at its own age
    /// and up to a ceiling, in one call; deciding that its arguments are valid before it reads the store.
    /// </summary>
    /// <remarks>
    /// This is an optional role interface, separate from the core outbox interfaces, so outboxes that cannot
    /// count by topic continue to work and report only their overall count.
    /// </remarks>
    public interface IAmATopicCountingOutbox
    {
        /// <summary>
        /// Counts the undispatched messages older than each requested topic's age, for every requested topic,
        /// in one call.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The result has one entry for each requested topic, and no other. A requested topic with no stale
        /// message gets 0, and an empty map gets an empty result. With no concurrent writers, each value is the
        /// number of stale messages on that topic, stopped at <paramref name="maxCountPerTopic"/>; a topic that
        /// reaches the ceiling does not limit the count of another.
        /// </para>
        /// <para>
        /// A message is counted by its header topic, <see cref="MessageHeader.Topic"/>, compared with each
        /// requested topic as <see cref="RoutingKey"/> compares values. Each age is applied on the outbox's own
        /// clock, with the same boundary as its overall count. Topics whose circuit breaker is tripped are not
        /// excluded.
        /// </para>
        /// <para>
        /// <paramref name="args"/> never narrows the requested topics or changes their values, and the call
        /// never changes <paramref name="args"/>, because the same dictionary may be passed by the posting path.
        /// </para>
        /// <para>
        /// No message body is transferred. Where the store can count, it counts on the server; otherwise it loads
        /// at most <paramref name="maxCountPerTopic"/> entries per topic, each without its body.
        /// </para>
        /// <para>
        /// The call is safe while other calls run on the same outbox instance, including the overall count and
        /// the calls of the posting path.
        /// </para>
        /// <para>
        /// The arguments are checked before the store is read. Each age must be greater than zero and no greater
        /// than 365 days, the maximum stale age the outbox stale-message monitor accepts.
        /// </para>
        /// </remarks>
        /// <param name="dispatchedSinceByTopic">Each topic to count, with the age a message on it must reach before it is stale.</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>
        /// <param name="maxCountPerTopic">The ceiling for each topic's count; the outbox stops counting a topic when it reaches it</param>
        /// <param name="args">Additional parameters required by some outbox implementations, if any</param>
        /// <param name="cancellationToken">Async Cancellation Token</param>
        /// <returns>The number of stale messages for each requested topic, stopped at <paramref name="maxCountPerTopic"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="dispatchedSinceByTopic"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// A requested topic is null or empty, or an age is not greater than zero or is greater than 365 days.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxCountPerTopic"/> is less than 1.</exception>
        /// <exception cref="OperationCanceledException">
        /// <paramref name="cancellationToken"/> is cancelled before the store answers; a store client may throw
        /// its own cancellation exception instead.
        /// </exception>
        Task<IReadOnlyDictionary<RoutingKey, int>> GetOutstandingMessageCountsByTopicAsync(
            IReadOnlyDictionary<RoutingKey, TimeSpan> dispatchedSinceByTopic,
            RequestContext? requestContext,
            int maxCountPerTopic,
            Dictionary<string, object>? args = null,
            CancellationToken cancellationToken = default);
    }
}
