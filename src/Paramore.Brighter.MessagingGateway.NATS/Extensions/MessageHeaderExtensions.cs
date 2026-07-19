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

using NATS.Client.Core;

namespace Paramore.Brighter.MessagingGateway.NATS.Extensions;

internal static class MessageHeaderExtensions
{
    public static NatsHeaders ToNatsHeaders(this MessageHeader messageHeader)
    {
        var headers = new NatsHeaders();
        headers[HeadersName.Id] = messageHeader.MessageId.Value;
        headers[HeadersName.Baggage] = messageHeader.Baggage.ToString();
        headers[HeadersName.ContentType] = messageHeader.ContentType.ToString();
        headers[HeadersName.CorrelationId] = messageHeader.CorrelationId.Value;
        headers[HeadersName.MessageType] = messageHeader.MessageType.ToString();
        headers[HeadersName.SpecVersion] = messageHeader.SpecVersion;
        headers[HeadersName.Source] = messageHeader.Source.ToString();
        headers[HeadersName.Time] = messageHeader.TimeStamp.ToString("O");

        if (messageHeader.DataSchema != null)
        {
            headers[HeadersName.DataSchema] = messageHeader.DataSchema.ToString();
        }

        if (messageHeader.DataRef != null)
        {
            headers[HeadersName.DataRef] = messageHeader.DataRef;
        }

        if (messageHeader.JobId != null)
        {
            headers[HeadersName.JobId] = messageHeader.JobId.ToString();
        }
        
        if (!RoutingKey.IsNullOrEmpty(messageHeader.ReplyTo))
        {
            headers[HeadersName.ReplyTo] = messageHeader.ReplyTo.Value;
        }

        if (messageHeader.Subject != null)
        {
            headers[HeadersName.Subject] = messageHeader.Subject;
        }

        if (messageHeader.TraceParent != null)
        {
            headers[HeadersName.TraceParent] = messageHeader.TraceParent.Value;
        }

        if (messageHeader.TraceState != null)
        {
            headers[HeadersName.TraceState] = messageHeader.TraceState.Value;
        }

        if (!string.IsNullOrEmpty(messageHeader.Type?.Value))
        {
            headers[HeadersName.Type] = messageHeader.Type.Value;
        }

        if (messageHeader.WorkflowId != null)
        {
            headers[HeadersName.WorkflowId] = messageHeader.WorkflowId.ToString();
        }

        foreach (var keyPair in messageHeader.Baggage)
        {
            if(keyPair.Value != null)
            {
                headers[keyPair.Key] = keyPair.Value;
            }
        }

        return headers;
    }
}
