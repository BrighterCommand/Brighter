#region Licence

/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Linq;
using System.Text;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.MessagingGateway.RESTMS.Model;

namespace Paramore.Brighter.MessagingGateway.RESTMS
{
    internal static class RestMSMessageCreator
    {
        public static Message CreateMessage(RestMSMessage restMSMessage)
        {
            var header = ReadMessageHeaders(restMSMessage);
            var body = ReadMessageBody(restMSMessage);

            return new Message(header, body);
        }

        private static MessageBody ReadMessageBody(RestMSMessage restMSMessage)
        {
            var srcEncoding = Encoding.GetEncoding(0);
            if (restMSMessage.Content.Encoding == "QuotedPrintable" || restMSMessage.Content.Encoding == "Plain")
            {
                srcEncoding = Encoding.ASCII;
            }
            //TODO: Handle base64 messages which are allowed by specification

            var bytes = srcEncoding.GetBytes(restMSMessage.Content.Value);
            var body = Encoding.Convert(srcEncoding, Encoding.Unicode, bytes);
            return new MessageBody(Encoding.Unicode.GetString(body));
        }

        private static MessageHeader ReadMessageHeaders(RestMSMessage restMSMessage)
        {
            var messageId = Guid.Empty;
            if (!Guid.TryParse(restMSMessage.MessageId, out messageId))
                return FailureMessageHeader(messageId, restMSMessage.Address);

            var messageType = MessageType.MT_NONE;
            if (restMSMessage.Headers == null || !restMSMessage.Headers.Any())
                return FailureMessageHeader(messageId, restMSMessage.Address);

            var messageTypeString = restMSMessage.Headers.First(header => header.Name == "MessageType").Value;
            if (messageTypeString == null)
                return FailureMessageHeader(messageId, restMSMessage.Address);

            if (!Enum.TryParse<MessageType>(messageTypeString, out messageType))
                return FailureMessageHeader(messageId, restMSMessage.Address);

            var messageHeader = new MessageHeader(messageId, restMSMessage.Address, messageType);
            restMSMessage.Headers.Each(header => messageHeader.Bag.Add(header.Name, header.Value));
            return messageHeader;
        }

        private static MessageHeader FailureMessageHeader(Guid messageId, string topic)
        {
            return new MessageHeader(messageId, topic, MessageType.MT_UNACCEPTABLE);
        }
    }
}
