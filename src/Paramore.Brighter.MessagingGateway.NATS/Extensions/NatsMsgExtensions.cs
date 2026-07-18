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

using System;
using System.Net.Mime;
using NATS.Client.Core;
using NATS.Client.JetStream;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.MessagingGateway.NATS.Extensions;

internal static class NatsMsgExtensions
{
    public static Message ToMessage(this INatsMsg<byte[]> natsMsg)
    {
        var message = new Message
        {
            Body = new MessageBody(natsMsg.Data ?? []),
            Header = new MessageHeader(
                messageId: GetMessageId(natsMsg.Headers),
                baggage: GetBaggage(natsMsg.Headers),
                contentType: GetContentType(natsMsg.Headers),
                correlationId: GetCorrelationId(natsMsg.Headers),
                dataSchema: GetDataSchema(natsMsg.Headers),
                jobId: GetJobId(natsMsg.Headers),
                messageType: GetMessageType(natsMsg.Headers),
                source: GetSource(natsMsg.Headers),
                subject: GetSubject(natsMsg.Headers),
                timeStamp: GetTime(natsMsg.Headers),
                topic: new RoutingKey(natsMsg.Subject),
                traceParent: GetTraceParent(natsMsg.Headers),
                traceState: GetTraceState(natsMsg.Headers),
                workflowId: GetWorkflowId(natsMsg.Headers))
            {
                DataRef = GetDataRef(natsMsg.Headers), SpecVersion = GetSpecVersion(natsMsg.Headers)
            }
        };

        if (!string.IsNullOrEmpty(natsMsg.ReplyTo))
        {
            message.Header.ReplyTo = new RoutingKey(natsMsg.ReplyTo!);
        }

        if (natsMsg.Headers != null)
        {
            foreach (var keyPair in natsMsg.Headers)
            {
                if (keyPair.Value.Count == 1)
                {
                    message.Header.Bag[keyPair.Key] = keyPair.Value.ToString();
                }
                else
                {
                    message.Header.Bag[keyPair.Key] = keyPair.Value;
                }
            }
        }

        message.Header.Bag[HeadersName.NatsMessage] = natsMsg;
        return message;
    }

    public static Message ToMessage(this INatsJSMsg<byte[]> natsMsg)
    {
        var message = new Message
        {
            Body = new MessageBody(natsMsg.Data ?? []),
            Header = new MessageHeader(
                messageId: GetMessageId(natsMsg.Headers),
                baggage: GetBaggage(natsMsg.Headers),
                contentType: GetContentType(natsMsg.Headers),
                correlationId: GetCorrelationId(natsMsg.Headers),
                dataSchema: GetDataSchema(natsMsg.Headers),
                jobId: GetJobId(natsMsg.Headers),
                messageType: GetMessageType(natsMsg.Headers),
                source: GetSource(natsMsg.Headers),
                subject: GetSubject(natsMsg.Headers),
                timeStamp: GetTime(natsMsg.Headers),
                topic: new RoutingKey(natsMsg.Subject),
                traceParent: GetTraceParent(natsMsg.Headers),
                traceState: GetTraceState(natsMsg.Headers),
                workflowId: GetWorkflowId(natsMsg.Headers))
            {
                DataRef = GetDataRef(natsMsg.Headers), SpecVersion = GetSpecVersion(natsMsg.Headers)
            }
        };

        if (!string.IsNullOrEmpty(natsMsg.ReplyTo))
        {
            message.Header.ReplyTo = new RoutingKey(natsMsg.ReplyTo!);
        }

        if (natsMsg.Headers != null)
        {
            foreach (var keyPair in natsMsg.Headers)
            {
                if (keyPair.Value.Count == 1)
                {
                    message.Header.Bag[keyPair.Key] = keyPair.Value.ToString();
                }
                else
                {
                    message.Header.Bag[keyPair.Key] = keyPair.Value;
                }
            }
        }

        message.Header.Bag[HeadersName.NatsMessage] = natsMsg;
        return message;
    }

    private static Id GetMessageId(NatsHeaders? headers)
    {
        if (headers != null && headers.TryGetValue(HeadersName.Id, out var messageId) && messageId.Count == 1)
        {
            return Id.Create(messageId.ToString());
        }

        return Id.Random();
    }

    private static Baggage GetBaggage(NatsHeaders? headers)
    {
        if (headers != null && headers.TryGetValue(HeadersName.Baggage, out var baggage) && baggage.Count == 1)
        {
            return Baggage.FromString(baggage.ToString());
        }

        return new Baggage();
    }

    private static ContentType GetContentType(NatsHeaders? headers)
    {
        if (headers != null && headers.TryGetValue(HeadersName.ContentType, out var contentType) &&
            contentType.Count == 1)
        {
            return new ContentType(contentType.ToString());
        }

        return new ContentType("text/plain");
    }

    private static Id GetCorrelationId(NatsHeaders? headers)
    {
        if (headers != null && headers.TryGetValue(HeadersName.CorrelationId, out var correlationId) &&
            correlationId.Count == 1)
        {
            return Id.Create(correlationId.ToString());
        }

        return Id.Random();
    }


    private static Uri? GetDataSchema(NatsHeaders? headers)
    {
        if (headers != null
            && headers.TryGetValue(HeadersName.DataSchema, out var dataSchema)
            && dataSchema.Count == 1
            && Uri.TryCreate(dataSchema.ToString(), UriKind.RelativeOrAbsolute, out var uri))
        {
            return uri;
        }

        return null;
    }

    private static string? GetDataRef(NatsHeaders? headers)
    {
        if (headers != null
            && headers.TryGetValue(HeadersName.DataRef, out var dataRef)
            && dataRef.Count == 1)
        {
            return dataRef;
        }

        return null;
    }


    private static MessageType GetMessageType(NatsHeaders? headers)
    {
        if (headers != null
            && headers.TryGetValue(HeadersName.MessageType, out var correlationId)
            && correlationId.Count == 1
            && Enum.TryParse<MessageType>(correlationId.ToString(), true, out var messageType))
        {
            return messageType;
        }

        return MessageType.MT_EVENT;
    }

    private static Id? GetJobId(NatsHeaders? headers)
    {
        if (headers != null
            && headers.TryGetValue(HeadersName.JobId, out var jobId)
            && jobId.Count == 1)
        {
            return Id.Create(jobId.ToString());
        }

        return null;
    }

    private static string? GetSubject(NatsHeaders? headers)
    {
        if (headers != null
            && headers.TryGetValue(HeadersName.Subject, out var subject)
            && subject.Count == 1)
        {
            return subject.ToString();
        }

        return null;
    }

    private static string GetSpecVersion(NatsHeaders? headers)
    {
        if (headers != null && headers.TryGetValue(HeadersName.SpecVersion, out var specVersion) &&
            specVersion.Count == 1)
        {
            return specVersion.ToString();
        }

        return MessageHeader.DefaultSpecVersion;
    }


    private static Uri GetSource(NatsHeaders? headers)
    {
        if (headers != null
            && headers.TryGetValue(HeadersName.Source, out var source)
            && source.Count == 1
            && Uri.TryCreate(source.ToString(), UriKind.RelativeOrAbsolute, out var uri))
        {
            return uri;
        }

        return new Uri(MessageHeader.DefaultSource);
    }

    private static DateTimeOffset GetTime(NatsHeaders? headers)
    {
        if (headers != null
            && headers.TryGetValue(HeadersName.Time, out var source)
            && source.Count == 1
            && DateTimeOffset.TryParse(source.ToString(), out var time))
        {
            return time;
        }

        return DateTimeOffset.UtcNow;
    }

    private static TraceParent? GetTraceParent(NatsHeaders? headers)
    {
        if (headers != null
            && headers.TryGetValue(HeadersName.TraceParent, out var traceParent)
            && traceParent.Count == 1)
        {
            return new TraceParent(traceParent.ToString());
        }

        return null;
    }

    private static TraceState? GetTraceState(NatsHeaders? headers)
    {
        if (headers != null
            && headers.TryGetValue(HeadersName.TraceState, out var traceParent)
            && traceParent.Count == 1)
        {
            return new TraceState(traceParent.ToString());
        }

        return null;
    }

    private static Id? GetWorkflowId(NatsHeaders? headers)
    {
        if (headers != null
            && headers.TryGetValue(HeadersName.WorkflowId, out var jobId)
            && jobId.Count == 1)
        {
            return Id.Create(jobId.ToString());
        }

        return null;
    }
}
