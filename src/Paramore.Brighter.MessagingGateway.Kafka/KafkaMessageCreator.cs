﻿#region Licence
/* The MIT License (MIT)
Copyright © 2024 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Globalization;
using System.Linq;
using System.Text;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.MessagingGateway.Kafka
{
    /// <summary>
    /// Turns a Kafka message into a Brighter message
    /// Kafka header values are a key and a byte[]. For known header values we can coerce back into the expected
    /// type. For unknown values, we just add them into the MessageHeader's Bag with a type of string. You will need
    /// to coerce them to the appropriate type yourself. You can set the serializer, so your alternative is to
    /// override the bag creation code to use more specific types.
    /// </summary>
    public class KafkaMessageCreator
    {
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<KafkaMessageCreator>();

        public Message CreateMessage(ConsumeResult<string, byte[]> consumeResult)
        {
            var topic = HeaderResult<string>.Empty();
            var messageId = HeaderResult<string>.Empty();
            var timeStamp = HeaderResult<DateTime>.Empty();
            var messageType = HeaderResult<MessageType>.Empty();
            var correlationId = HeaderResult<string>.Empty();
            var partitionKey = HeaderResult<string>.Empty();
            var replyTo = HeaderResult<string>.Empty();
            var contentType = HeaderResult<string>.Empty();
            var delayMilliseconds = HeaderResult<int>.Empty();
            var handledCount = HeaderResult<int>.Empty();

            Message message;
            try
            {
                topic = ReadTopic(consumeResult.Topic);
                messageId = ReadMessageId(consumeResult.Message.Headers);
                timeStamp = ReadTimeStamp(consumeResult.Message.Headers);
                messageType = ReadMessageType(consumeResult.Message.Headers);
                correlationId = ReadCorrelationId(consumeResult.Message.Headers);
                partitionKey = ReadPartitionKey(consumeResult.Message.Headers);
                replyTo = ReadReplyTo(consumeResult.Message.Headers);
                contentType = ReadContentType(consumeResult.Message.Headers);
                delayMilliseconds = ReadDelayMilliseconds(consumeResult.Message.Headers);
                handledCount = ReadHandledCount(consumeResult.Message.Headers);

                if (false == (topic.Success && messageId.Success && messageType.Success && timeStamp.Success))
                {
                    message = FailureMessage(topic, messageId);
                }
                else
                {
                    var messageHeader = timeStamp.Success
                        ? new MessageHeader(messageId.Result, topic.Result, messageType.Result, timeStamp.Result)
                        : new MessageHeader(messageId.Result, topic.Result, messageType.Result);

                    if (correlationId.Success)
                        messageHeader.CorrelationId = correlationId.Result;

                    //If we don't have a partition key in the header, assume a non-Brighter sender and use message key
                    if (partitionKey.Success)
                        messageHeader.PartitionKey = partitionKey.Result;
                    else
                        messageHeader.PartitionKey = consumeResult.Message.Key;

                    if (contentType.Success)
                        messageHeader.ContentType = contentType.Result;

                    if (replyTo.Success)
                        messageHeader.ReplyTo = replyTo.Result;

                    if (delayMilliseconds.Success)
                        messageHeader.DelayedMilliseconds = delayMilliseconds.Result;

                    if (handledCount.Success)
                        messageHeader.HandledCount = handledCount.Result;

                    message = new Message(messageHeader,
                        new MessageBody(consumeResult.Message.Value, messageHeader.ContentType));

                    if (!message.Header.Bag.ContainsKey(HeaderNames.PARTITION_OFFSET))
                        message.Header.Bag.Add(HeaderNames.PARTITION_OFFSET, consumeResult.TopicPartitionOffset);

                    consumeResult.Message.Headers.Each(header =>
                    {
                        ReadBagEntry(header, message);
                    });
                }
            }
            catch (Exception e)
            {
                s_logger.LogWarning(e, "Failed to create message from Kafka offset");
                message = FailureMessage(topic, messageId);
            }

            return message;
        }

        private Message FailureMessage(HeaderResult<string> topic, HeaderResult<string> messageId)
        {
            var header = new MessageHeader(
                messageId.Success ? messageId.Result : string.Empty,
                topic.Success ? topic.Result : string.Empty,
                MessageType.MT_UNACCEPTABLE);
            var message = new Message(header, new MessageBody(string.Empty));
            return message;
        }

        private HeaderResult<string> ReadContentType(Headers headers)
        {
            return ReadHeader(headers, HeaderNames.CONTENT_TYPE, false);
        }

        private HeaderResult<string> ReadCorrelationId(Headers headers)
        {
            return ReadHeader(headers, HeaderNames.CORRELATION_ID)
                .Map(correlationId =>
                {
                    if (string.IsNullOrEmpty(correlationId))
                    {
                        s_logger.LogDebug("No correlation id found in message");
                        return new HeaderResult<string>(string.Empty, true);
                    }

                    return new HeaderResult<string>(correlationId, true);

                });
        }

        private HeaderResult<int> ReadDelayMilliseconds(Headers headers)
        {
            return ReadHeader(headers, HeaderNames.DELAYED_MILLISECONDS)
                .Map(s =>
                {
                    if (string.IsNullOrEmpty(s))
                    {
                        s_logger.LogDebug("No delay milliseconds found in message");
                        return new HeaderResult<int>(0, true);
                    }

                    if (int.TryParse(s, out int delayMilliseconds))
                    {
                        return new HeaderResult<int>(delayMilliseconds, true);
                    }

                    s_logger.LogDebug("Could not parse message delayMilliseconds: {DelayMillisecondsValue}", s);
                    return new HeaderResult<int>(0, false);
                });
        }

        private HeaderResult<int> ReadHandledCount(Headers headers)
        {
            return ReadHeader(headers, HeaderNames.HANDLED_COUNT)
                .Map(s =>
                {
                    if (string.IsNullOrEmpty(s))
                    {
                        s_logger.LogDebug("No handled count found in message");
                        return new HeaderResult<int>(0, true);
                    }

                    if (int.TryParse(s, out int handledCount))
                    {
                        return new HeaderResult<int>(handledCount, true);
                    }

                    s_logger.LogDebug("Could not parse message handled count: {HandledCountValue}", s);
                    return new HeaderResult<int>(0, false);
                });
        }

        private HeaderResult<string> ReadReplyTo(Headers headers)
        {
            return ReadHeader(headers, HeaderNames.REPLY_TO)
                .Map(s =>
                {
                    if (string.IsNullOrEmpty(s))
                    {
                        s_logger.LogDebug("No reply to found in message");
                        return new HeaderResult<string>(string.Empty, true);
                    }

                    return new HeaderResult<string>(s, true);
                });
        }

        private HeaderResult<DateTime> ReadTimeStamp(Headers headers)
        {
            if (headers.TryGetLastBytesIgnoreCase(HeaderNames.TIMESTAMP, out byte[] lastHeader))
            {
                //Additional testing for a non unixtimestamp string
                if (DateTime.TryParse(lastHeader.FromByteArray(), DateTimeFormatInfo.CurrentInfo,
                        DateTimeStyles.AdjustToUniversal, out DateTime timestamp))
                {
                    return new HeaderResult<DateTime>(timestamp, true);
                }

                try
                {
                    return new HeaderResult<DateTime>(
                        DateTimeOffset.FromUnixTimeMilliseconds(BitConverter.ToInt64(lastHeader, 0)).DateTime, true);
                }
                catch (Exception)
                {
                    return new HeaderResult<DateTime>(DateTime.UtcNow, true);
                }
            }

            return new HeaderResult<DateTime>(DateTime.UtcNow, true);
        }

        private HeaderResult<MessageType> ReadMessageType(Headers headers)
        {
            return ReadHeader(headers, HeaderNames.MESSAGE_TYPE)
                .Map(s =>
                {
                    if (string.IsNullOrEmpty(s))
                    {
                        return new HeaderResult<MessageType>(MessageType.MT_EVENT, true);
                    }

                    var success = Enum.TryParse(s, true, out MessageType result);
                    return new HeaderResult<MessageType>(result, success);
                });
        }

        private HeaderResult<string> ReadTopic(string topic)
        {
            return new HeaderResult<string>(topic, true);
        }

        private HeaderResult<string> ReadMessageId(Headers headers)
        {
            var newMessageId = Guid.NewGuid().ToString();

            return ReadHeader(headers, HeaderNames.MESSAGE_ID)
                .Map(messageId =>
                {
                    if (string.IsNullOrEmpty(messageId))
                    {
                        s_logger.LogDebug("No message id found in message MessageId, new message id is {NewMessageId}", newMessageId);
                        return new HeaderResult<string>(newMessageId, true);
                    }

                    return new HeaderResult<string>(messageId, true);
                });
        }

        private HeaderResult<string> ReadPartitionKey(Headers headers)
        {
            return ReadHeader(headers, HeaderNames.PARTITIONKEY)
                .Map(s =>
                {
                    if (string.IsNullOrEmpty(s))
                    {
                        s_logger.LogDebug("No partition key found in message");
                        return new HeaderResult<string>(string.Empty, false);
                    }

                    return new HeaderResult<string>(s, true);
                });
        }

        private HeaderResult<string> ReadHeader(Headers headers, string key, bool dieOnMissing = false)
        {
            if (headers.TryGetLastBytesIgnoreCase(key, out byte[] lastHeader))
            {
                try
                {
                    var val = lastHeader.FromByteArray();
                    return new HeaderResult<string>(val, true);
                }
                catch (Exception e)
                {
                    var firstTwentyBytes = BitConverter.ToString(lastHeader.Take(20).ToArray());
                    s_logger.LogWarning(e,
                        "Failed to read the value of header {Topic} as UTF-8, first 20 byes follow: \n\t{1}", key,
                        firstTwentyBytes);
                    return new HeaderResult<string>(null, false);
                }
            }

            return new HeaderResult<string>(string.Empty, !dieOnMissing);
        }

        /// <summary>
        /// Override this in a derived class if you want to coerce specific user defined  header values to the correct
        /// type in the bag
        /// </summary>
        /// <param name="header">The Kafka message header</param>
        /// <param name="message">The Brighter message</param>
        protected virtual void ReadBagEntry(IHeader header, Message message)
        {
            if (!BrighterDefinedHeaders.HeadersToReset.Any(htr => htr.Equals(header.Key)))
                message.Header.Bag.Add(header.Key, Encoding.UTF8.GetString(header.GetValueBytes()));
        }
    }
}
