#region Licence

/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Collections.Generic;
using System.Net.Mime;

using Paramore.Brighter.Observability;

namespace Paramore.Brighter.AWS.V4.Tests;

/// <summary>
/// A message builder for FIFO queues that preserves the default partition key
/// when <see cref="PartitionKey.Empty"/> is passed. FIFO queues require a
/// MessageGroupId which is derived from the partition key.
/// </summary>
public class FifoMessageBuilder : IAmAMessageBuilder
{
    private readonly DefaultMessageBuilder _inner = new();

    /// <inheritdoc />
    public IAmAMessageBuilder SetBag(Dictionary<string, object> bag) { _inner.SetBag(bag); return this; }

    /// <inheritdoc />
    public IAmAMessageBuilder SetBaggage(Baggage baggage) { _inner.SetBaggage(baggage); return this; }

    /// <inheritdoc />
    public IAmAMessageBuilder SetContentType(ContentType contentType) { _inner.SetContentType(contentType); return this; }

    /// <inheritdoc />
    public IAmAMessageBuilder SetCorrelationId(Id correlationId) { _inner.SetCorrelationId(correlationId); return this; }

    /// <inheritdoc />
    public IAmAMessageBuilder SetDataSchema(Uri? dataSchema) { _inner.SetDataSchema(dataSchema); return this; }

    /// <inheritdoc />
    public IAmAMessageBuilder SetDataRef(string? dataRef) { _inner.SetDataRef(dataRef); return this; }

    /// <inheritdoc />
    public IAmAMessageBuilder SetDelayed(TimeSpan? delayed) { _inner.SetDelayed(delayed); return this; }

    /// <inheritdoc />
    public IAmAMessageBuilder SetJobId(Id? jobId) { _inner.SetJobId(jobId); return this; }

    /// <inheritdoc />
    public IAmAMessageBuilder SetMessageId(Id? messageId) { _inner.SetMessageId(messageId); return this; }

    /// <inheritdoc />
    public IAmAMessageBuilder SetMessageType(MessageType messageType) { _inner.SetMessageType(messageType); return this; }

    /// <inheritdoc />
    /// <remarks>
    /// Ignores <see cref="PartitionKey.Empty"/> to preserve the default partition key,
    /// ensuring FIFO queues always have a MessageGroupId.
    /// </remarks>
    public IAmAMessageBuilder SetPartitionKey(PartitionKey partitionKey)
    {
        if (!PartitionKey.IsNullOrEmpty(partitionKey))
        {
            _inner.SetPartitionKey(partitionKey);
        }

        return this;
    }

    /// <inheritdoc />
    public IAmAMessageBuilder SetReplyTo(RoutingKey? replyTo) { _inner.SetReplyTo(replyTo); return this; }

    /// <inheritdoc />
    public IAmAMessageBuilder SetSubject(string? subject) { _inner.SetSubject(subject); return this; }

    /// <inheritdoc />
    public IAmAMessageBuilder SetSpecVersion(string? specVersion) { _inner.SetSpecVersion(specVersion); return this; }

    /// <inheritdoc />
    public IAmAMessageBuilder SetSource(Uri? source) { _inner.SetSource(source); return this; }

    /// <inheritdoc />
    public IAmAMessageBuilder SetTopic(RoutingKey topic) { _inner.SetTopic(topic); return this; }

    /// <inheritdoc />
    public IAmAMessageBuilder SetTimeStamp(DateTimeOffset timeStamp) { _inner.SetTimeStamp(timeStamp); return this; }

    /// <inheritdoc />
    public IAmAMessageBuilder SetTraceParent(TraceParent? traceParent) { _inner.SetTraceParent(traceParent); return this; }

    /// <inheritdoc />
    public IAmAMessageBuilder SetTraceState(TraceState? traceState) { _inner.SetTraceState(traceState); return this; }

    /// <inheritdoc />
    public IAmAMessageBuilder SetType(CloudEventsType type) { _inner.SetType(type); return this; }

    /// <inheritdoc />
    public IAmAMessageBuilder SetWorkflowId(Id? workflowId) { _inner.SetWorkflowId(workflowId); return this; }

    /// <inheritdoc />
    public IAmAMessageBuilder SetBody(byte[] body) { _inner.SetBody(body); return this; }

    /// <inheritdoc />
    public Message Build() => _inner.Build();
}
