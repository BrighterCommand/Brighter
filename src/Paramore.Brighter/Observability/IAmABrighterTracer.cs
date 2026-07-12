#region Licence

/* The MIT License (MIT)
Copyright © 2024 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

namespace Paramore.Brighter.Observability;

public interface IAmABrighterTracer : IDisposable
{
    /// <summary>
    /// The ActivitySource for the tracer
    /// </summary>
    ActivitySource ActivitySource { get; }
    
    /// <summary>
    /// Create a span when we consume a message from a queue or stream. If the message has no propagated
    /// trace context, the span is created as a root span instead of inheriting the long-running pump span.
    /// </summary>
    /// <param name="operation">How did we obtain the message. InstrumentationOptions.Receive => pull; InstrumentationOptions.Process => push</param>
    /// <param name="message">What is the <see cref="Message"/> that we received; if they have a traceparentid we will use that as a parent for this trace</param>
    /// <param name="messagingSystem">What is the messaging system that we are receiving a message from</param>
    /// <param name="options">The <see cref="InstrumentationOptions"/> for how deep should the instrumentation go</param>
    /// <param name="serializedHeader">The header already serialized by <see cref="EnrichReceiveSpan"/>, reused here instead
    /// of serializing it again; when null (no preceding receive span) the header is serialized by this method</param>
    /// <returns></returns>
    Activity? CreateSpan(
        MessagePumpSpanOperation operation,
        Message message,
        MessagingSystem messagingSystem,
        InstrumentationOptions options = InstrumentationOptions.All,
        string? serializedHeader = null
    );

    /// <summary>
    /// Creates a root receive span before the broker call so that the span's <see cref="Activity.Duration"/> reflects
    /// only broker latency without inheriting the long-running pump span. Tags derived from the received
    /// <see cref="Message"/> are added later via <see cref="EnrichReceiveSpan"/>.
    /// </summary>
    /// <param name="topic">The <see cref="RoutingKey"/> we are receiving from</param>
    /// <param name="messagingSystem">The <see cref="MessagingSystem"/> we are receiving from</param>
    /// <param name="options">The <see cref="InstrumentationOptions"/> for how deep should the instrumentation go</param>
    /// <returns>The receive span, or null if the <see cref="ActivitySource"/> has no listeners</returns>
    Activity? CreateReceiveSpan(
        RoutingKey topic,
        MessagingSystem messagingSystem,
        InstrumentationOptions options = InstrumentationOptions.All);

    /// <summary>
    /// Enriches a receive span (created via <see cref="CreateReceiveSpan"/>) with tags derived from a received message,
    /// and propagates the producer's tracestate onto the receive span.
    /// </summary>
    /// <param name="span">The receive span to enrich; no-op if null</param>
    /// <param name="message">The <see cref="Message"/> that was received</param>
    /// <param name="options">The <see cref="InstrumentationOptions"/> for how deep should the instrumentation go</param>
    /// <returns>The serialized header, to pass to <see cref="CreateSpan(MessagePumpSpanOperation,Message,MessagingSystem,InstrumentationOptions,string)"/>
    /// so it is not serialized again; null if there was no span to enrich or messaging instrumentation is disabled</returns>
    string? EnrichReceiveSpan(
        Activity? span,
        Message message,
        InstrumentationOptions options = InstrumentationOptions.All);

    /// <summary>
    /// Propagates the producer's baggage onto the consumer side for a received message: lifts the message's
    /// <see cref="MessageHeader.CorrelationId"/> into its <see cref="MessageHeader.Baggage"/> and sets it as the ambient
    /// OpenTelemetry baggage for the current execution context. Called by the pump for a received message, gated on a
    /// receive span existing (sampled in, instrumentation enabled), mirroring the historic span-scoped propagation.
    /// </summary>
    /// <param name="message">The <see cref="Message"/> that was received</param>
    void PropagateConsumerContext(Message message);

    /// <summary>
    /// Create a span for a request in CommandProcessor
    /// </summary>
    /// <param name="operation">What type of span are we creating</param>
    /// <param name="request">What is the request that we are tracking with this span</param>
    /// <param name="parentActivity">The parent activity, if any, that we should assign to this span</param>
    /// <param name="links">Are there links to other spans that we should add to this span</param>
    /// <param name="options">How deep should the instrumentation go?</param>
    /// <returns>A span (or dotnet Activity) for the current request named request.name operation.name</returns>
    Activity? CreateSpan<TRequest>(
        CommandProcessorSpanOperation operation, 
        TRequest request, 
        Activity? parentActivity = null,
        ActivityLink[]? links = null, 
        InstrumentationOptions options = InstrumentationOptions.All
    ) where TRequest : class, IRequest;
    
    /// <summary>
    /// Creates a span for an archive operation. Because a sweeper may not create an externa bus, but just use the archiver directly, we
    /// check for this existing and then recreate directly in the archive provider if it does not exist
    /// </summary>
    /// <param name="parentActivity">A parent activity that called this one</param>
    /// <param name="dispatchedSince">The minimum age of a row to be archived</param>
    /// <param name="options">The <see cref="InstrumentationOptions"/> for how deep should the instrumentation go?</param>
    /// <returns></returns>
    Activity? CreateArchiveSpan(
        Activity? parentActivity, 
        TimeSpan dispatchedSince, 
        InstrumentationOptions options = InstrumentationOptions.All
        );
    
    /// <summary>
    /// Create a span for an inbox or outbox operation
    /// </summary>
    /// <param name="info">The attributes of the claim check operation</param>
    /// <param name="options">How deep should the instrumentation go?</param>
    /// <returns>A new span named either db.operation db.name db.sql.table or db.operation db.name if db.sql.table not available </returns>
    Activity? CreateClaimCheckSpan(ClaimCheckSpanInfo info, InstrumentationOptions options = InstrumentationOptions.All);
    
    /// <summary>
    /// Create a span for a request in CommandProcessor
    /// </summary>
    /// <param name="parentActivity">The parent activity, if any, that we should assign to this span</param>
    /// <param name="links">Are there links to other spans that we should add to this span</param>
    /// <param name="options">How deep should the instrumentation go?</param>
    /// <returns>A span (or dotnet Activity) for the current request named request.name operation.name</returns>
    Activity? CreateBatchSpan<TRequest>(
        Activity? parentActivity = null,
        ActivityLink[]? links = null, 
        InstrumentationOptions options = InstrumentationOptions.All
    ) where TRequest : class, IRequest;
    
    /// <summary>
    /// The parent span for the message pump. This is the entry point for the message pump
    /// </summary>
    /// <param name="operation">The <see cref="MessagePumpSpanOperation"/>. This should be Begin or End</param>
    /// <param name="topic">The <see cref="RoutingKey"/> for this span</param>
    /// <param name="messagingSystem">The <see cref="MessagingSystem"/> that we are receiving from</param>
    /// <param name="options">The <see cref="InstrumentationOptions"/> for how deep should the instrumentation go?</param>
    /// <returns>A span (or dotnet Activity) for the current request named request.name operation.name</returns>
    Activity? CreateMessagePumpSpan(
        MessagePumpSpanOperation operation,
        RoutingKey topic,
        MessagingSystem messagingSystem,
        InstrumentationOptions options = InstrumentationOptions.All);

    /// <param name="messagePumpException"></param>
    /// <param name="topic">The <see cref="RoutingKey"/> for this span</param>
    /// <param name="operation">The <see cref="MessagingSystem"/> we were trying to perform</param>
    /// <param name="messagingSystem">The <see cref="MessagingSystem"/> that we are receiving from</param>
    /// <param name="options">The <see cref="InstrumentationOptions"/> for how deep should the instrumentation go?</param>
    /// <returns>A span (or dotnet Activity) for the current request named request.name operation.name</returns>
    Activity? CreateMessagePumpExceptionSpan(
        Exception messagePumpException,
        RoutingKey topic,
        MessagePumpSpanOperation operation,
        MessagingSystem messagingSystem,
        InstrumentationOptions options = InstrumentationOptions.All);

    /// <summary>
    /// Create a span for a batch of messages to be cleared  
    /// </summary>
    /// <param name="operation">The operation being performed as part of the Clear Span; can be "create" to start a batch
    /// or "clear" for an individual item in a batch</param>
    /// <param name="parentActivity">The span that forms a parent to this span</param>
    /// <param name="messageId">The identifier of the message that we want to clear</param>
    /// <param name="options">How verbose do we want the trace to be?</param>
    /// <returns></returns>
    Activity? CreateClearSpan(
        CommandProcessorSpanOperation operation, 
        Activity? parentActivity,
        string? messageId = null,
        InstrumentationOptions options = InstrumentationOptions.All
    );

    /// <summary>
    /// Create a span for an inbox or outbox operation
    /// </summary>
    /// <param name="info">The attributes of the db operation</param>
    /// <param name="parentActivity">The parent activity, if any, that we should assign to this span</param>
    /// <param name="options">How deep should the instrumentation go?</param>
    /// /// <returns>A new span named either db.operation db.name db.sql.table or db.operation db.name if db.sql.table not available </returns>
    Activity? CreateDbSpan(
        BoxSpanInfo info, 
        Activity? parentActivity, 
        InstrumentationOptions options = InstrumentationOptions.All
    );
    
    /// <summary>
    /// Create a span that represents Brighter producing a message to a channel
    /// </summary>
    /// <param name="publication">The publication which represents where we are sending the message</param>
    /// <param name="message">The message that we are sending</param>
    /// <param name="parentActivity">The parent activity, if any, that we should assign to this span</param>
    /// <param name="instrumentationOptions">How deep should the instrumentation go?</param>
    /// <returns>A new span named channel publish</returns>
    Activity? CreateProducerSpan(
        Publication publication,
        Message? message,
        Activity? parentActivity,
        InstrumentationOptions instrumentationOptions = InstrumentationOptions.All);

    /// <summary>
    /// Create a standalone span that represents a broker confirmation (ack/nack) of a previously produced message.
    /// The span links back to the original publish span (via <paramref name="links"/>) rather than nesting under it,
    /// so the publish span is never reopened or mutated.
    /// </summary>
    /// <remarks>
    /// This method clears <see cref="Activity.Current"/> to force the confirmation span to be a true root and does
    /// NOT restore the prior ambient activity — it deliberately leaves the confirmation span as the new
    /// <see cref="Activity.Current"/> so that work done in the callback (e.g. the success-branch <c>MarkDispatched</c>
    /// span) nests beneath it. It is therefore only safe to call from a context whose ambient activity is owned and
    /// disposed by the caller (such as a confirmation callback running on a broker/threadpool thread). Calling it
    /// inline on a thread that owns a meaningful ambient span would silently lose that <see cref="Activity.Current"/>.
    /// </remarks>
    /// <param name="messageId">The id of the message the broker confirmed; <see cref="Id.Empty"/> records an "unknown" marker</param>
    /// <param name="topic">The wire topic the message was published to, recorded as the messaging destination</param>
    /// <param name="success">True if the broker confirmed persistence; false records the failure as an error outcome</param>
    /// <param name="links">Links to the original publish span, if its context was captured at send time; null when absent</param>
    /// <param name="options">How deep should the instrumentation go?</param>
    /// <returns>The confirmation span, which also becomes <see cref="Activity.Current"/>, or null if the source has no listeners</returns>
    Activity? CreateConfirmationSpan(
        Id messageId,
        RoutingKey? topic,
        bool success,
        ActivityLink[]? links = null,
        InstrumentationOptions options = InstrumentationOptions.All);

    /// <summary>
    /// Ends a span by correctly setting its status and then disposing of it
    /// </summary>
    /// <param name="span">The span to end</param>
    void EndSpan(Activity? span);
    
    /// <summary>
    /// Ends a collection of named spans
    /// </summary>
    /// <param name="handlerSpans"></param>
    void EndSpans(ConcurrentDictionary<string, Activity> handlerSpans);

    /// <summary>
    /// Links together a collection of spans
    /// Mainly used with a batch to link siblings to each other
    /// </summary>
    /// <param name="handlerSpans"></param>
    void LinkSpans(ConcurrentDictionary<string, Activity> handlerSpans);

    /// <summary>
    /// If an activity has an exception, then we should record it on the span
    /// </summary>
    /// <param name="span"></param>
    /// <param name="exceptions"></param>
    void AddExceptionToSpan(Activity? span, IEnumerable<Exception> exceptions);
}
