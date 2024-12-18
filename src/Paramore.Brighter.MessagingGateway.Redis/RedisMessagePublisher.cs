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
using System.Text;
using System.Text.Json;

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
            
            //Read Message Id
            WriteMessageId(messageHeader, headers);
            //Read TimeStamp
            WriteTimeStamp(messageHeader, headers);
            //Read Topic
            WriteTopic(messageHeader, headers);
            //Read MessageType
            WriteMessageType(messageHeader, headers);
           //Read HandledCount
            WriteHandledCount(messageHeader, headers);
            //Read DelayedMilliseconds
            WriteDelayedMilliseconds(messageHeader, headers);
            //Read MessageBag
            WriteMessageBag(messageHeader, headers);
            //reply to
            WrtiteReplyTo(messageHeader, headers);
            //content type
            WriteContentType(messageHeader, headers);
            //correlation id
            WriteCorrelationId(messageHeader, headers);

            return JsonSerializer.Serialize(headers, JsonSerialisationOptions.Options);
        }

        private void WriteContentType(MessageHeader messageHeader, Dictionary<string, string> headers)
        {
            headers.Add(HeaderNames.CONTENT_TYPE, messageHeader.ContentType ?? "text/plain");
        }

        private void WriteCorrelationId(MessageHeader messageHeader, Dictionary<string, string> headers)
        {
            headers.Add(HeaderNames.CORRELATION_ID, messageHeader.CorrelationId);
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

        private void WriteTopic(MessageHeader messageHeader, Dictionary<string, string> headers)
        {
            headers.Add(HeaderNames.TOPIC, messageHeader.Topic);
        }

        private void WriteTimeStamp(MessageHeader messageHeader, Dictionary<string, string> headers)
        {
            headers.Add(HeaderNames.TIMESTAMP, JsonSerializer.Serialize(messageHeader.TimeStamp, JsonSerialisationOptions.Options));
        }


        public void Dispose()
        {
            _writer.Dispose();
        }
    }
}
