#region Licence
/* The MIT License (MIT)
Copyright © 2014 Bob Gregory 

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
using System.Linq;
using System.Text;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.MessagingGateway.RMQ.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Paramore.Brighter.MessagingGateway.RMQ
{
    internal class RmqMessageCreator
    {
        private static readonly Lazy<ILog> _logger = new Lazy<ILog>(LogProvider.For<RmqMessageCreator>);

        private HeaderResult<string> ReadHeader(IDictionary<string, object> dict, string key, bool dieOnMissing = false)
        {
            if (false == dict.ContainsKey(key))
            {
                return new HeaderResult<string>(string.Empty, !dieOnMissing);
            }

            var bytes = dict[key] as byte[];
            if (null == bytes)
            {
                _logger.Value.WarnFormat("The value of header {0} could not be cast to a byte array", key);
                return new HeaderResult<string>(null, false);
            }

            try
            {
                var val = Encoding.UTF8.GetString(bytes);
                return new HeaderResult<string>(val, true);
            }
            catch (Exception e)
            {
                var firstTwentyBytes = BitConverter.ToString(bytes.Take(20).ToArray());
                _logger.Value.WarnException("Failed to read the value of header {0} as UTF-8, first 20 byes follow: \n\t{1}", e, key, firstTwentyBytes);
                return new HeaderResult<string>(null, false);
            }
        }

        public Message CreateMessage(BasicDeliverEventArgs fromQueue)
        {
            var headers = fromQueue.BasicProperties.Headers ?? new Dictionary<string, object>();
            var topic = HeaderResult<string>.Empty();
            var messageId = HeaderResult<Guid>.Empty();
            var timeStamp = HeaderResult<DateTime>.Empty();
            var handledCount = HeaderResult<int>.Empty();
            var delayedMilliseconds = HeaderResult<int>.Empty();
            var redelivered = HeaderResult<bool>.Empty();
            var deliveryTag = HeaderResult<ulong>.Empty();
            var messageType = HeaderResult<MessageType>.Empty();
            var replyTo = HeaderResult<string>.Empty();

            Message message;
            try
            {
                topic = ReadTopic(fromQueue, headers);
                messageId = ReadMessageId(fromQueue.BasicProperties.MessageId);
                timeStamp = ReadTimeStamp(fromQueue.BasicProperties);
                handledCount = ReadHandledCount(headers);
                delayedMilliseconds = ReadDelayedMilliseconds(headers);
                redelivered = ReadRedeliveredFlag(fromQueue.Redelivered);
                deliveryTag = ReadDeliveryTag(fromQueue.DeliveryTag);
                messageType = ReadMessageType(headers);
                replyTo = ReadReplyTo(fromQueue.BasicProperties);

                if (false == (topic.Success && messageId.Success && messageType.Success && timeStamp.Success && handledCount.Success))
                {
                    message = FailureMessage(topic, messageId);
                }
                else
                {
                    var messageHeader = timeStamp.Success
                        ? new MessageHeader(messageId.Result, topic.Result, messageType.Result, timeStamp.Result, handledCount.Result, delayedMilliseconds.Result)
                        : new MessageHeader(messageId.Result, topic.Result, messageType.Result);

                    message = new Message(messageHeader, new MessageBody(fromQueue.Body, fromQueue.BasicProperties.Type));

                    headers.Each(header => message.Header.Bag.Add(header.Key, ParseHeaderValue(header.Value)));
                }

                if (headers.ContainsKey(HeaderNames.CORRELATION_ID))
                {
                    var correlationId = Encoding.UTF8.GetString((byte[])headers[HeaderNames.CORRELATION_ID]);
                    message.Header.CorrelationId = Guid.Parse(correlationId);
                }

                message.DeliveryTag = deliveryTag.Result;
                message.Redelivered = redelivered.Result;
                message.Header.ReplyTo = replyTo.Result;
            }
            catch (Exception e)
            {
                _logger.Value.WarnException("Failed to create message from amqp message", e);
                message = FailureMessage(topic, messageId);
            }
            
            return message;
        }


        private Message FailureMessage(HeaderResult<string> topic, HeaderResult<Guid> messageId)
        {
            var header = new MessageHeader(
                messageId.Success ? messageId.Result : Guid.Empty,
                topic.Success ? topic.Result : string.Empty,
                MessageType.MT_UNACCEPTABLE);
            var message = new Message(header, new MessageBody(string.Empty));
            return message;
        }

        private HeaderResult<ulong> ReadDeliveryTag(ulong deliveryTag)
        {
            return new HeaderResult<ulong>(deliveryTag, true);
        }

        private HeaderResult<DateTime> ReadTimeStamp(IBasicProperties basicProperties)
        {
            if (basicProperties.IsTimestampPresent())
            {
                return new HeaderResult<DateTime>(UnixTimestamp.DateTimeFromUnixTimestampSeconds(basicProperties.Timestamp.UnixTime), true);
            }

            return new HeaderResult<DateTime>(DateTime.UtcNow, true);
        }

        private HeaderResult<MessageType> ReadMessageType(IDictionary<string, object> headers)
        {
            return ReadHeader(headers, HeaderNames.MESSAGE_TYPE)
                .Map(s =>
                {
                    if (string.IsNullOrEmpty(s))
                    {
                        return new HeaderResult<MessageType>(MessageType.MT_EVENT, true);
                    }
                    MessageType result;
                    var success = Enum.TryParse(s, true, out result);
                    return new HeaderResult<MessageType>(result, success);
                });
        }

        private HeaderResult<int> ReadHandledCount(IDictionary<string, object> headers)
        {
            return ReadHeader(headers, HeaderNames.HANDLED_COUNT).Map(s =>
            {
                if (string.IsNullOrEmpty(s))
                {
                    return new HeaderResult<int>(0, true);
                }
                int handledCount;
                var val = int.TryParse(s, out handledCount) ? handledCount : 0;
                return new HeaderResult<int>(val, true);
            });
        }

        private HeaderResult<int> ReadDelayedMilliseconds(IDictionary<string, object> headers)
        {
            return ReadHeader(headers, HeaderNames.DELAYED_MILLISECONDS).Map(s =>
            {
                if (string.IsNullOrEmpty(s))
                {
                    return new HeaderResult<int>(-1, true);
                }

                int delayedMilliseconds;

                if (int.TryParse(s, out delayedMilliseconds))
                    return new HeaderResult<int>(delayedMilliseconds > 0 ? -1 : (delayedMilliseconds * -1), true);
                else
                    return new HeaderResult<int>(-1, false);
            });
        }

        private HeaderResult<string> ReadTopic(BasicDeliverEventArgs fromQueue, IDictionary<string, object> headers)
        {
            return ReadHeader(headers, HeaderNames.TOPIC).Map(s =>
            {
                var val = string.IsNullOrEmpty(s) ? fromQueue.RoutingKey : s;
                return new HeaderResult<string>(val, true);
            });
        }

        private HeaderResult<Guid> ReadMessageId(string messageId)
        {
            var newMessageId = Guid.NewGuid();

            if (string.IsNullOrEmpty(messageId))
            {
                _logger.Value.DebugFormat("No message id found in message MessageId, new message id is {0}", newMessageId);
                return new HeaderResult<Guid>(newMessageId, true);
            }

            if (Guid.TryParse(messageId, out newMessageId))
            {
                return new HeaderResult<Guid>(newMessageId, true);
            }

            _logger.Value.DebugFormat("Could not parse message MessageId, new message id is {0}", Guid.Empty);
            return new HeaderResult<Guid>(Guid.Empty, false);
        }

        private HeaderResult<bool> ReadRedeliveredFlag(bool redelivered)
        {
           return new HeaderResult<bool>(redelivered, true); 
        }

        private HeaderResult<string> ReadReplyTo(IBasicProperties basicProperties)
        {
            if (basicProperties.IsReplyToPresent())
            {
                return new HeaderResult<string>(basicProperties.ReplyTo, true);
            }

            return new HeaderResult<string>(null, true);
 
        }

        private static object ParseHeaderValue(object value)
        {
            var bytes = value as byte[];
            return bytes != null ? Encoding.UTF8.GetString(bytes) : value;
        }

    }
}
