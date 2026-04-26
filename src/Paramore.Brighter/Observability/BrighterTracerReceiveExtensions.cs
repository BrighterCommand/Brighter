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
using System.Diagnostics;
using System.Text.Json;
using Paramore.Brighter.JsonConverters;

namespace Paramore.Brighter.Observability;

/// <summary>
/// Extension methods that produce the consumer-side receive span. Provided as extensions on
/// <see cref="IAmABrighterTracer"/> rather than members of the interface so that adding the receive flow does not break
/// existing implementations of <see cref="IAmABrighterTracer"/>.
/// </summary>
public static class BrighterTracerReceiveExtensions
{
    /// <summary>
    /// Creates a receive span before the broker call so that the span's <see cref="Activity.Duration"/> reflects only
    /// broker latency. Tags derived from the message are added later via
    /// <see cref="EnrichReceiveSpan"/>.
    /// </summary>
    /// <param name="tracer">The <see cref="IAmABrighterTracer"/>; null returns null without throwing</param>
    /// <param name="topic">The <see cref="RoutingKey"/> we are receiving from</param>
    /// <param name="messagingSystem">The <see cref="MessagingSystem"/> we are receiving from</param>
    /// <param name="options">The <see cref="InstrumentationOptions"/> for how deep should the instrumentation go</param>
    /// <param name="timeProvider">Optional <see cref="TimeProvider"/> used as the span start time; defaults to <see cref="TimeProvider.System"/></param>
    /// <returns>The receive span, or null if the tracer is null or the <see cref="ActivitySource"/> has no listeners</returns>
    public static Activity? CreateReceiveSpan(
        this IAmABrighterTracer? tracer,
        RoutingKey topic,
        MessagingSystem messagingSystem,
        InstrumentationOptions options = InstrumentationOptions.All,
        TimeProvider? timeProvider = null)
    {
        if (tracer is null) return null;

        var operation = MessagePumpSpanOperation.Receive;
        var tags = new ActivityTagsCollection();
        if (options != InstrumentationOptions.None)
            tags.Add(BrighterSemanticConventions.InstrumentationDomain, BrighterSemanticConventions.MessagingInstrumentationDomain);
        if (options.HasFlag(InstrumentationOptions.RequestInformation))
            tags.Add(BrighterSemanticConventions.MessagingOperationType, operation.ToSpanName());
        if (options.HasFlag(InstrumentationOptions.Messaging))
        {
            tags.Add(BrighterSemanticConventions.MessagingDestination, topic);
            tags.Add(BrighterSemanticConventions.MessagingSystem, messagingSystem.ToMessagingSystemName());
            tags.Add(BrighterSemanticConventions.Operation, operation.ToSpanName());
        }

        var activity = tracer.ActivitySource.StartActivity(
            name: $"{topic} {operation.ToSpanName()}",
            kind: ActivityKind.Consumer,
            tags: tags,
            startTime: (timeProvider ?? TimeProvider.System).GetUtcNow());

        if (activity is not null)
            Activity.Current = activity;

        return activity;
    }

    /// <summary>
    /// Enriches a receive span (created via <see cref="CreateReceiveSpan"/>) with tags derived from a received message,
    /// and propagates the producer's tracestate and baggage onto the consumer side.
    /// </summary>
    /// <param name="tracer">The <see cref="IAmABrighterTracer"/>; receiver is unused but kept for call-site symmetry</param>
    /// <param name="span">The receive span to enrich; no-op if null</param>
    /// <param name="message">The <see cref="Message"/> that was received</param>
    /// <param name="options">The <see cref="InstrumentationOptions"/> for how deep should the instrumentation go</param>
    public static void EnrichReceiveSpan(
        this IAmABrighterTracer? tracer,
        Activity? span,
        Message message,
        InstrumentationOptions options = InstrumentationOptions.All)
    {
        _ = tracer;
        if (span is null) return;

        if (options.HasFlag(InstrumentationOptions.RequestInformation))
        {
            span.AddTag(BrighterSemanticConventions.CeType, message.Header.Type.Value);
            span.AddTag(BrighterSemanticConventions.ReplyTo, message.Header.ReplyTo?.Value);
            span.AddTag(BrighterSemanticConventions.HandledCount, message.Header.HandledCount);
            span.AddTag(BrighterSemanticConventions.CeMessageId, message.Id.Value);
            span.AddTag(BrighterSemanticConventions.CeSource, message.Header.Source);
            span.AddTag(BrighterSemanticConventions.CeVersion, "1.0");
            span.AddTag(BrighterSemanticConventions.CeSubject, message.Header.Subject);
        }

        if (options.HasFlag(InstrumentationOptions.Messaging))
        {
            span.AddTag(BrighterSemanticConventions.MessagingDestinationPartitionId, message.Header.PartitionKey.Value);
            span.AddTag(BrighterSemanticConventions.MessageId, message.Id.Value);
            span.AddTag(BrighterSemanticConventions.MessageType, message.Header.MessageType.ToString());
            span.AddTag(BrighterSemanticConventions.MessageBodySize, message.Body.Bytes.Length);
            span.AddTag(BrighterSemanticConventions.MessageHeaders, JsonSerializer.Serialize(message.Header, JsonSerialisationOptions.Options));
            span.AddTag(BrighterSemanticConventions.ConversationId, message.Header.CorrelationId.Value);
        }

        if (options.HasFlag(InstrumentationOptions.RequestBody))
            span.AddTag(BrighterSemanticConventions.MessageBody, message.Body.Value);

        // Propagate the producer's tracestate and baggage onto the consumer side. Done here (not in CreateReceiveSpan) because
        // these values come from the message and aren't known until the broker call returns. Mirrors what CreateSpan(Process, ...)
        // already does for serviceable messages — needed here so MT_UNACCEPTABLE rejections still carry the producer trace context.
        if (!string.IsNullOrEmpty(message.Header.TraceState?.Value))
            span.TraceStateString = message.Header.TraceState!.Value;

        if (!string.IsNullOrEmpty(message.Header.CorrelationId))
            message.Header.Baggage.Add("correlationId", message.Header.CorrelationId.Value);
        OpenTelemetry.Baggage.SetBaggage(message.Header.Baggage);
    }
}
