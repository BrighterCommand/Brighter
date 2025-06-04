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
    /// Create a span when we consume a message from a queue or stream
    /// </summary>
    /// <param name="operation">How did we obtain the message. InstrumentationOptions.Receive => pull; InstrumentationOptions.Process => push</param>
    /// <param name="message">What is the <see cref="Message"/> that we received; if they have a traceparentid we will use that as a parent for this trace</param>
    /// <param name="messagingSystem">What is the messaging system that we are receiving a message from</param>
    /// <param name="options">The <see cref="InstrumentationOptions"/> for how deep should the instrumentation go</param>
    /// <returns></returns>
    Activity? CreateSpan(
        MessagePumpSpanOperation operation,
        Message message,
        MessagingSystem messagingSystem,
        InstrumentationOptions options = InstrumentationOptions.All
    );

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
    /// <returns>A new span named either db.operation db.name db.sql.table or db.operation db.name if db.sql.table not available </returns>
    Activity? CreateClaimCheckSpan(ClaimCheckSpanInfo info);
    
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
