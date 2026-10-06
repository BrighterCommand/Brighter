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

using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter
{
    /// <summary>
    /// Role: An inbox that tracks the causation id of the requests it stores.
    /// Responsibility: Knowing which causation an inbox entry belongs to.
    /// </summary>
    /// <remarks>
    /// This is an optional role interface, separate from the core inbox interfaces, so inboxes that do not
    /// support causation tracking continue to work. The causation id links an inbox entry to the outbox
    /// messages produced during that handler invocation, enabling <see cref="Paramore.Brighter.Inbox.OnceOnlyAction.Replay"/>.
    /// </remarks>
    public interface IAmACausationTrackingInbox
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
        /// Gets the causation id stored for an inbox entry.
        /// </summary>
        /// <param name="id">The id of the stored request.</param>
        /// <param name="contextKey">The context key that disambiguates the request (such as the handler name).</param>
        /// <param name="requestContext">The context for this request; used to access the Span.</param>
        /// <param name="timeoutInMilliseconds">A timeout for the operation, -1 for no timeout.</param>
        /// <returns>The causation id, or null if none is stored.</returns>
        string? GetCausationId(string id, string contextKey,
            RequestContext? requestContext, int timeoutInMilliseconds = -1);

        /// <summary>
        /// Gets the causation id stored for an inbox entry. Async counterpart of <see cref="GetCausationId"/>.
        /// </summary>
        /// <param name="id">The id of the stored request.</param>
        /// <param name="contextKey">The context key that disambiguates the request (such as the handler name).</param>
        /// <param name="requestContext">The context for this request; used to access the Span.</param>
        /// <param name="timeoutInMilliseconds">A timeout for the operation, -1 for no timeout.</param>
        /// <param name="cancellationToken">Allows the caller to cancel the operation.</param>
        /// <returns>The causation id, or null if none is stored.</returns>
        Task<string?> GetCausationIdAsync(string id, string contextKey,
            RequestContext? requestContext, int timeoutInMilliseconds = -1,
            CancellationToken cancellationToken = default);
    }
}
