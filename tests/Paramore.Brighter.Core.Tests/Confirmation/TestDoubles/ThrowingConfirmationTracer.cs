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
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Core.Tests.Confirmation.TestDoubles
{
    /// <summary>
    /// A tracer test double that throws while starting or ending a confirmation span to exercise
    /// the confirmation callback's error-isolation path (NFR-4 / AC-14). Every other member is an
    /// inert no-op.
    /// </summary>
    internal sealed class ThrowingConfirmationTracer(bool throwOnEndSpan = false) : IAmABrighterTracer
    {
        public ActivitySource ActivitySource { get; } = new("Paramore.Brighter.Tests.Throwing");

        public Activity? CreateConfirmationSpan(
            Id messageId,
            RoutingKey? topic,
            bool success,
            ActivityLink[]? links = null,
            InstrumentationOptions options = InstrumentationOptions.All)
        {
            if (!throwOnEndSpan)
                throw new InvalidOperationException("Observability failure injected by the test");

            return null;
        }

        public Activity? CreateSpan(MessagePumpSpanOperation operation, Message message, MessagingSystem messagingSystem,
            InstrumentationOptions options = InstrumentationOptions.All, string? serializedHeader = null) => null;

        public Activity? CreateReceiveSpan(RoutingKey topic, MessagingSystem messagingSystem,
            InstrumentationOptions options = InstrumentationOptions.All) => null;

        public string? EnrichReceiveSpan(Activity? span, Message message,
            InstrumentationOptions options = InstrumentationOptions.All) => null;

        public void PropagateConsumerContext(Message message) { }

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

        public void EndSpan(Activity? span)
        {
            if (throwOnEndSpan)
                throw new InvalidOperationException("Observability failure injected by the test");
        }

        public void EndSpans(ConcurrentDictionary<string, Activity> handlerSpans) { }

        public void LinkSpans(ConcurrentDictionary<string, Activity> handlerSpans) { }

        public void AddExceptionToSpan(Activity? span, IEnumerable<Exception> exceptions) { }

        public void Dispose() => ActivitySource.Dispose();
    }
}
