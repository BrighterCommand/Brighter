// The MIT License (MIT)
// Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
//  of this software and associated documentation files (the "Software"), to deal
//  in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
//  The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

namespace Paramore.Brighter.MessagingGateway.NATS;

/// <summary>
/// Well-known NATS header names used when mapping between Brighter message headers and NATS headers.
/// CloudEvents properties use the <c>ce-</c> prefix; Brighter-specific properties use the <c>brighter-</c> prefix.
/// </summary>
public static class HeadersName
{
    /// <summary>The CloudEvents id header carrying the message id.</summary>
    public const string Id = "ce-id";

    /// <summary>The CloudEvents baggage header carrying the W3C baggage.</summary>
    public const string Baggage = "ce-baggage";

    /// <summary>The CloudEvents datacontenttype header carrying the content type.</summary>
    public const string ContentType = "ce-datacontenttype";

    /// <summary>The CloudEvents correlation id header.</summary>
    public const string CorrelationId = "ce-correlationid";

    /// <summary>The CloudEvents dataschema header.</summary>
    public const string DataSchema = "ce-dataschema";

    /// <summary>The CloudEvents dataref header carrying a claim-check reference.</summary>
    public const string DataRef = "ce-dataref";

    /// <summary>The Brighter header carrying the job id.</summary>
    public const string JobId = "brighter-jobid";

    /// <summary>The Brighter header carrying the <see cref="MessageType"/>.</summary>
    public const string MessageType = "brighter-messagetype";

    /// <summary>The CloudEvents reply-to header.</summary>
    public const string ReplyTo = "ce-replyto";

    /// <summary>The CloudEvents subject header.</summary>
    public const string Subject = "ce-subject";

    /// <summary>The CloudEvents specversion header.</summary>
    public const string SpecVersion  = "ce-specversion";

    /// <summary>The CloudEvents source header.</summary>
    public const string Source  = "ce-source";

    /// <summary>The CloudEvents time header carrying the message timestamp.</summary>
    public const string Time = "ce-time";

    /// <summary>The CloudEvents traceparent header carrying the W3C trace parent.</summary>
    public const string TraceParent = "ce-traceparent";

    /// <summary>The CloudEvents tracestate header carrying the W3C trace state.</summary>
    public const string TraceState = "ce-tracestate";

    /// <summary>The Brighter header carrying the workflow id.</summary>
    public const string WorkflowId ="brighter-workflowid";

    /// <summary>The message-bag key under which the original NATS message is stored on the Brighter message.</summary>
    public const string NatsMessage = "BrighterNats";
}
