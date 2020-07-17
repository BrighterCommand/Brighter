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

using System.Collections.Generic;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace Paramore.Brighter.MessagingGateway.RedisStreams
{
    public class RedisStreamsPublisher
    {
        public static NameValueEntry[] Create(Message message)
        {
           var entries = new List<NameValueEntry>();
           WriteHeader(message.Header, entries);
           WriteBody(message.Body, entries);
           return entries.ToArray();
        }

        private static void WriteBody(MessageBody messageBody, ICollection<NameValueEntry> entries)
        {
            entries.Add( new NameValueEntry("body", messageBody.Value)); 
        }

        private static void WriteHeader(MessageHeader messageHeader, ICollection<NameValueEntry> entries)
        {
            //Read Message Id
            WriteMessageId(messageHeader, entries);
            //Read TimeStamp
            WriteTimeStamp(messageHeader, entries);
            //Read Topic
            WriteTopic(messageHeader, entries);
            //Read MessageType
            WriteMessageType(messageHeader, entries);
           //Read HandledCount
            WriteHandledCount(messageHeader, entries);
            //Read DelayedMilliseconds
            WriteDelayedMilliseconds(messageHeader, entries);
            //Read MessageBag
            WriteMessageBag(messageHeader, entries);
            //reply to
            WrtiteReplyTo(messageHeader, entries);
            //content type
            WriteContentType(messageHeader, entries);
            //correlation id
            WriteCorrelationId(messageHeader, entries);
        }

        private static void WriteContentType(MessageHeader messageHeader, ICollection<NameValueEntry> entries)
        {
            entries.Add( new NameValueEntry(MessageNames.CONTENT_TYPE, messageHeader.ContentType.ToString()));
        }

        private static void WriteCorrelationId(MessageHeader messageHeader, ICollection<NameValueEntry> entries)
        {
            entries.Add(new NameValueEntry(MessageNames.CORRELATION_ID, messageHeader.CorrelationId.ToString()));
        }

        private static void WriteDelayedMilliseconds(MessageHeader messageHeader, ICollection<NameValueEntry> entries)
        {
            entries.Add(new NameValueEntry(MessageNames.DELAYED_MILLISECONDS, messageHeader.DelayedMilliseconds.ToString()));
        }
        
        private static void WriteHandledCount(MessageHeader messageHeader, ICollection<NameValueEntry> entries)
        {
            entries.Add(new NameValueEntry(MessageNames.HANDLED_COUNT, messageHeader.HandledCount.ToString()));
        }

        private static void WriteMessageBag(MessageHeader messageHeader, ICollection<NameValueEntry> entries)
        {
            var flatBag = JsonConvert.SerializeObject(messageHeader.Bag);
            entries.Add(new NameValueEntry(MessageNames.BAG, flatBag));
        }

        private static void WriteMessageId(MessageHeader messageHeader, ICollection<NameValueEntry> entries)
        {
            entries.Add(new NameValueEntry(MessageNames.MESSAGE_ID, messageHeader.Id.ToString()));
        }
        
        private static void WriteMessageType(MessageHeader messageHeader, ICollection<NameValueEntry> entries)
        {
            entries.Add(new NameValueEntry(MessageNames.MESSAGE_TYPE, messageHeader.MessageType.ToString()));
        }
        
        private static void WrtiteReplyTo(MessageHeader messageHeader, ICollection<NameValueEntry> entries)
        {
            entries.Add(new NameValueEntry(MessageNames.REPLY_TO, messageHeader.ReplyTo));
        }

        private static void WriteTopic(MessageHeader messageHeader, ICollection<NameValueEntry> entries)
        {
            entries.Add(new NameValueEntry(MessageNames.TOPIC, messageHeader.Topic));
        }

        private static void WriteTimeStamp(MessageHeader messageHeader, ICollection<NameValueEntry> entries)
        {
            entries.Add(new NameValueEntry(MessageNames.TIMESTAMP, JsonConvert.SerializeObject(messageHeader.TimeStamp)));
        }
    }
}
