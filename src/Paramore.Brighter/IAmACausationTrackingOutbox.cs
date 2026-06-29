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

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter
{
    /// <summary>
    /// Role: An outbox that can replay messages for a causation.
    /// Responsibility: Knowing if causation tracking is supported, and doing the reset of dispatch state
    /// for a causation's outbox messages.
    /// </summary>
    /// <remarks>
    /// This is an optional role interface, separate from the core outbox interfaces, so outboxes that do not
    /// support causation tracking continue to work. Replaying a causation clears the dispatched state of the
    /// outbox messages produced under that causation, so the sweeper resends them.
    /// </remarks>
    public interface IAmACausationTrackingOutbox
    {
        /// <summary>
        /// Does the live store schema support causation tracking? This is a runtime check so that a user who
        /// upgrades Brighter but has not migrated the store schema gets a clear validation error at startup.
        /// </summary>
        /// <returns>True if the schema supports causation tracking.</returns>
        bool SupportsCausationTracking();

        /// <summary>
        /// Does the live store schema support causation tracking? Async counterpart of <see cref="SupportsCausationTracking"/>.
        /// </summary>
        /// <param name="cancellationToken">Allows the caller to cancel the operation.</param>
        /// <returns>True if the schema supports causation tracking.</returns>
        Task<bool> SupportsCausationTrackingAsync(
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Replays the outbox messages produced under a causation by clearing their dispatched state so the
        /// sweeper resends them.
        /// </summary>
        /// <param name="causationId">The causation id whose messages should be replayed.</param>
        /// <param name="requestContext">The context for this request; used to access the Span.</param>
        /// <param name="args">Optional bag of arguments required by some outbox implementations.</param>
        void ReplayCausation(string causationId, RequestContext? requestContext,
            Dictionary<string, object>? args = null);

        /// <summary>
        /// Replays the outbox messages produced under a causation. Async counterpart of <see cref="ReplayCausation"/>.
        /// </summary>
        /// <param name="causationId">The causation id whose messages should be replayed.</param>
        /// <param name="requestContext">The context for this request; used to access the Span.</param>
        /// <param name="args">Optional bag of arguments required by some outbox implementations.</param>
        /// <param name="cancellationToken">Allows the caller to cancel the operation.</param>
        Task ReplayCausationAsync(string causationId, RequestContext? requestContext,
            Dictionary<string, object>? args = null,
            CancellationToken cancellationToken = default);
    }
}
