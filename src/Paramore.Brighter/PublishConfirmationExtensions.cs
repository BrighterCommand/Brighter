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

using System;
using System.Threading.Tasks;

namespace Paramore.Brighter
{
    /// <summary>
    /// Helpers for raising publish-confirmation callbacks. Public so that Brighter's transport
    /// packages can consume them across the assembly boundary; producers implementing
    /// <see cref="ISupportPublishConfirmationAsync"/> are the intended callers.
    /// </summary>
    public static class PublishConfirmationExtensions
    {
        /// <summary>
        /// Invokes every subscriber of an <see cref="ISupportPublishConfirmationAsync.OnMessagePublishedAsync"/>
        /// event, awaiting each in subscription order. This is the canonical raise for the awaited
        /// confirmation event: sequential (never concurrent) invocation, over a snapshot of the
        /// invocation list. A null <paramref name="handlers"/> (no subscribers) is a no-op.
        /// A throwing subscriber propagates its exception and skips the remaining subscribers for
        /// that confirmation — callers (the producers) wrap the whole raise in a per-confirmation
        /// catch, so a fault is contained to one confirmation, not one subscriber.
        /// </summary>
        /// <param name="handlers">The event's backing delegate; may be null or multicast.</param>
        /// <param name="result">The confirmation to deliver to each subscriber.</param>
        public static async Task InvokeAllAsync(this Func<PublishConfirmationResult, Task>? handlers, PublishConfirmationResult result)
        {
            if (handlers is null)
                return;

            foreach (Func<PublishConfirmationResult, Task> handler in handlers.GetInvocationList())
                await handler(result);
        }
    }
}
