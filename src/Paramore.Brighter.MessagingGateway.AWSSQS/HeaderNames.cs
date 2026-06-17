#region Licence

/* The MIT License (MIT)
Copyright © 2022 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using System.Collections.Generic;

namespace Paramore.Brighter.MessagingGateway.AWSSQS;

/// <summary>
/// The names of Headers
/// </summary>
public static class HeaderNames
{
    public const string Id = "id";
    public const string Topic = "topic";
    public const string ContentType = "content-type";
    public const string CorrelationId = "correlation-id";
    public const string HandledCount = "handled-count";
    public const string MessageType = "message-type";
    public const string Timestamp = "timestamp";
    public const string ReplyTo = "reply-to";
    public const string Subject = "subject";
    public const string Bag = "bag";
    public const string DeduplicationId = "messageDeduplicationId";
    public const string Type = "type";
    public const string SpecVersion = "specversion";
    public const string Source = "souce";
    public const string Time = "time";
    public const string DataContentType = "datacontenttype";
    public const string DataSchema = "dataschema";
    public const string DataRef = "dataref";
    public const string TraceState = "tracestate";
    public const string TraceParent = "traceparent";
    public const string Baggage = "baggage";

    /// <summary>
    /// Use this because we cannot set cloud events as individual headers, SNS/SQS can only have 10 headers in raw message delivery
    /// and instead need a single header with all cloud events for example,
    /// </summary>
    public const string CloudEventHeaders = "cloudeventheaders";

    /// <summary>
    /// SNS/SQS MessageAttribute names that Brighter reads into typed
    /// <see cref="MessageHeader"/> fields (or its own JSON-serialised bag). Inbound
    /// attributes whose names are <em>not</em> in this set are surfaced into
    /// <see cref="MessageHeader.Bag"/> under their raw name so interop with foreign
    /// producers is preserved instead of silently discarding their metadata.
    /// Comparison is ordinal — AWS message attribute names are case-sensitive.
    /// </summary>
    private static readonly HashSet<string> s_knownNames = new(System.StringComparer.Ordinal)
    {
        Id, Topic, ContentType, CorrelationId, HandledCount, MessageType, Timestamp,
        ReplyTo, Subject, Bag, DeduplicationId, Type, SpecVersion, Source, Time,
        DataContentType, DataSchema, DataRef, TraceState, TraceParent, Baggage,
        CloudEventHeaders
    };

    /// <summary>
    /// Returns <c>true</c> when <paramref name="name"/> is one of the SNS/SQS
    /// MessageAttribute names Brighter consumes directly into a typed
    /// <see cref="MessageHeader"/> field or its JSON-serialised bag.
    /// </summary>
    public static bool IsKnown(string name) => s_knownNames.Contains(name);
}
