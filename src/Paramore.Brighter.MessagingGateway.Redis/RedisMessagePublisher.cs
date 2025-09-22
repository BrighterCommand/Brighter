#region Licence
/* The MIT License (MIT)
Copyright © 2017 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using Paramore.Brighter.JsonConverters;

namespace Paramore.Brighter.MessagingGateway.Redis
{
    public class RedisMessagePublisher: IDisposable
    {
        public const string EMPTY_MESSAGE = "<HEADER HEADER/><BODY BODY/>";
        private const string BEGINNING_OF_HEADER = "<HEADER ";
        private const string END_OF_HEADER = " HEADER/>";
        private const string BEGINNING_OF_BODY = "<BODY";
        private const string END_OF_BODY = "BODY/>";
        
        private readonly StringWriter _writer = new(new StringBuilder());

        public string Create(Message message)
        {
           WriteHeader(message.Header);
           WriteBody(message.Body);
           return _writer.ToString();
        }

        private void WriteBody(MessageBody messageBody)
        {
            _writer.WriteLine(BEGINNING_OF_BODY); 
            
            _writer.WriteLine(messageBody.Value);
            
            _writer.WriteLine(END_OF_BODY); 
        }

        private void WriteHeader(MessageHeader messageHeader)
        {
            _writer.WriteLine(BEGINNING_OF_HEADER);
            
            _writer.WriteLine(FlattenHeader(messageHeader));
            
            _writer.WriteLine(END_OF_HEADER);
        }

        private string FlattenHeader(MessageHeader messageHeader)
        {
            var headers = new Dictionary<string, string>();
            
            WriteMessageId(messageHeader, headers);
            WriteTimeStamp(messageHeader, headers);
            WriteTopic(messageHeader, headers);
            WriteMessageType(messageHeader, headers);
            WriteHandledCount(messageHeader, headers);
            WriteDelayedMilliseconds(messageHeader, headers);
            WriteMessageBag(messageHeader, headers);
            WrtiteReplyTo(messageHeader, headers);
            WriteContentType(messageHeader, headers);
            WriteCorrelationId(messageHeader, headers);
            WriteSource(messageHeader, headers);
            WriteType(messageHeader, headers);
            WriteDataSchema(messageHeader, headers);
            WriteSubject(messageHeader, headers);
            WriteTraceParent(messageHeader, headers);
            WriteTraceState(messageHeader, headers);
            WriteBaggage(messageHeader, headers);


            return JsonSerializer.Serialize(headers, JsonSerialisationOptions.Options);
        }
        
        private void WriteBaggage(MessageHeader messageHeader, Dictionary<string, string> headers)
        {
            headers.Add(HeaderNames.W3C_BAGGAGE, messageHeader.Baggage.ToString());
        }   

        private void WriteContentType(MessageHeader messageHeader, Dictionary<string, string> headers)
        {
            var contentType = messageHeader.ContentType is not null ? messageHeader.ContentType.ToString() : MediaTypeNames.Text.Plain;
            headers.Add(HeaderNames.CONTENT_TYPE, contentType);
        }

        private void WriteCorrelationId(MessageHeader messageHeader, Dictionary<string, string> headers)
        {
            headers.Add(HeaderNames.CORRELATION_ID, messageHeader.CorrelationId);
        }
        
        private void WriteDataSchema(MessageHeader messageHeader, Dictionary<string, string> headers)
        {
            if (messageHeader.DataSchema != null && messageHeader.DataSchema.IsAbsoluteUri)
                headers.Add(HeaderNames.CLOUD_EVENTS_DATA_SCHEMA, messageHeader.DataSchema.AbsoluteUri);
        }

        private void WriteDelayedMilliseconds(MessageHeader messageHeader, Dictionary<string, string> headers)
        {
            headers.Add(HeaderNames.DELAYED_MILLISECONDS, messageHeader.Delayed.TotalMilliseconds.ToString(CultureInfo.InvariantCulture));
        }
        
        private void WriteHandledCount(MessageHeader messageHeader, Dictionary<string, string> headers)
        {
            headers.Add(HeaderNames.HANDLED_COUNT, messageHeader.HandledCount.ToString());
        }

        private void WriteMessageBag(MessageHeader messageHeader, Dictionary<string, string> headers)
        {
            var flatBag = JsonSerializer.Serialize(messageHeader.Bag, JsonSerialisationOptions.Options);
            headers.Add(HeaderNames.BAG, flatBag);
        }

        private void WriteMessageId(MessageHeader messageHeader, Dictionary<string, string> headers)
        {
            headers.Add(HeaderNames.MESSAGE_ID, messageHeader.MessageId);
        }
        
        private void WriteMessageType(MessageHeader messageHeader, Dictionary<string, string> headers)
        {
            headers.Add(HeaderNames.MESSAGE_TYPE, messageHeader.MessageType.ToString());
        }
        
        private void WrtiteReplyTo(MessageHeader messageHeader, Dictionary<string, string> headers)
        {
            headers.Add(HeaderNames.REPLY_TO, messageHeader.ReplyTo ?? string.Empty);
        }
        
        private void WriteSource(MessageHeader messageHeader, Dictionary<string, string> headers)
        {
            headers.Add(HeaderNames.CLOUD_EVENTS_SOURCE, messageHeader.Source.AbsoluteUri);
        }

        private void WriteTopic(MessageHeader messageHeader, Dictionary<string, string> headers)
        {
            headers.Add(HeaderNames.TOPIC, messageHeader.Topic);
        }
        
        private void WriteSubject(MessageHeader messageHeader, Dictionary<string, string> headers)
        {
            if (!string.IsNullOrEmpty(messageHeader.Subject))
                headers.Add(HeaderNames.CLOUD_EVENTS_SUBJECT, messageHeader.Subject!);
        }
        
        private void WriteTraceParent(MessageHeader messageHeader, Dictionary<string, string> headers)
        {
            if (messageHeader.TraceParent != null)
                headers.Add(HeaderNames.CLOUD_EVENTS_TRACE_PARENT, messageHeader.TraceParent.Value);
        }
        
        private void WriteTraceState(MessageHeader messageHeader, Dictionary<string, string> headers)
        {
            if (messageHeader.TraceState != null)
                headers.Add(HeaderNames.CLOUD_EVENTS_TRACE_STATE, messageHeader.TraceState.Value);
        }

        private void WriteTimeStamp(MessageHeader messageHeader, Dictionary<string, string> headers)
        {
            headers.Add(HeaderNames.TIMESTAMP, JsonSerializer.Serialize(messageHeader.TimeStamp, JsonSerialisationOptions.Options));
        }
        
        private void WriteType(MessageHeader messageHeader, Dictionary<string, string> headers)
        {
            headers.Add(HeaderNames.CLOUD_EVENTS_TYPE, messageHeader.Type);
        }

        public void Dispose()
        {
            _writer.Dispose();
        }
    }
}
