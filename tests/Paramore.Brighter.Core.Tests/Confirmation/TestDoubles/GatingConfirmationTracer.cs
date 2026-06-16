#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */
#endregion

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Core.Tests.Confirmation.TestDoubles
{
    /// <summary>
    /// A tracer test double that rendezvouses every confirmation callback on a <see cref="Barrier"/>
    /// inside <see cref="CreateConfirmationSpan"/> (which the mediator invokes first on every
    /// confirmation). This forces all participants to be simultaneously in-flight before any of them
    /// proceeds to the breaker trip, so concurrent same-topic trips genuinely overlap (NFR-3 / AC-10).
    /// Every other member is an inert no-op.
    /// </summary>
    internal sealed class GatingConfirmationTracer : IAmABrighterTracer
    {
        private readonly Barrier _barrier;

        public GatingConfirmationTracer(Barrier barrier) => _barrier = barrier;

        public ActivitySource ActivitySource { get; } = new("Paramore.Brighter.Tests.Gating");

        public Activity? CreateConfirmationSpan(
            Id messageId,
            RoutingKey? topic,
            bool success,
            ActivityLink[]? links = null,
            InstrumentationOptions options = InstrumentationOptions.All)
        {
            // Block until every confirmation callback has reached this point, then release them all
            // together so the work that follows (the breaker trip) runs concurrently.
            _barrier.SignalAndWait();
            return null;
        }

        public Activity? CreateSpan(MessagePumpSpanOperation operation, Message message, MessagingSystem messagingSystem,
            InstrumentationOptions options = InstrumentationOptions.All) => null;

        public Activity? CreateReceiveSpan(RoutingKey topic, MessagingSystem messagingSystem,
            InstrumentationOptions options = InstrumentationOptions.All) => null;

        public void EnrichReceiveSpan(Activity? span, Message message,
            InstrumentationOptions options = InstrumentationOptions.All) { }

        public Activity? CreateSpan<TRequest>(CommandProcessorSpanOperation operation, TRequest request,
            Activity? parentActivity = null, ActivityLink[]? links = null,
            InstrumentationOptions options = InstrumentationOptions.All) where TRequest : class, IRequest => null;

        public Activity? CreateArchiveSpan(Activity? parentActivity, TimeSpan dispatchedSince,
            InstrumentationOptions options = InstrumentationOptions.All) => null;

        public Activity? CreateClaimCheckSpan(ClaimCheckSpanInfo info,
            InstrumentationOptions options = InstrumentationOptions.All) => null;

        public Activity? CreateBatchSpan<TRequest>(Activity? parentActivity = null, ActivityLink[]? links = null,
            InstrumentationOptions options = InstrumentationOptions.All) where TRequest : class, IRequest => null;

        public Activity? CreateMessagePumpSpan(MessagePumpSpanOperation operation, RoutingKey topic,
            MessagingSystem messagingSystem, InstrumentationOptions options = InstrumentationOptions.All) => null;

        public Activity? CreateMessagePumpExceptionSpan(Exception messagePumpException, RoutingKey topic,
            MessagePumpSpanOperation operation, MessagingSystem messagingSystem,
            InstrumentationOptions options = InstrumentationOptions.All) => null;

        public Activity? CreateClearSpan(CommandProcessorSpanOperation operation, Activity? parentActivity,
            string? messageId = null, InstrumentationOptions options = InstrumentationOptions.All) => null;

        public Activity? CreateDbSpan(BoxSpanInfo info, Activity? parentActivity,
            InstrumentationOptions options = InstrumentationOptions.All) => null;

        public Activity? CreateProducerSpan(Publication publication, Message? message, Activity? parentActivity,
            InstrumentationOptions instrumentationOptions = InstrumentationOptions.All) => null;

        public void EndSpan(Activity? span) { }

        public void EndSpans(ConcurrentDictionary<string, Activity> handlerSpans) { }

        public void LinkSpans(ConcurrentDictionary<string, Activity> handlerSpans) { }

        public void AddExceptionToSpan(Activity? span, IEnumerable<Exception> exceptions) { }

        public void Dispose() => ActivitySource.Dispose();
    }
}
