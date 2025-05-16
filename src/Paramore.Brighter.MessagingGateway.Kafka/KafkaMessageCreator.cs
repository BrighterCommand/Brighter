#region Licence

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
    public partial class KafkaMessageCreator
    {
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<KafkaMessageCreator>();

        public Message CreateMessage(ConsumeResult<string, byte[]> consumeResult)
        {
            var topic = HeaderResult<RoutingKey>.Empty();
            var messageId = HeaderResult<string>.Empty();

            Message message;
            try
            {
                topic = ReadTopic(consumeResult.Topic);
                messageId = ReadMessageId(consumeResult.Message.Headers);
                var timeStamp = ReadTimeStamp(consumeResult.Message.Headers);
                var messageType = ReadMessageType(consumeResult.Message.Headers);
                var correlationId = ReadCorrelationId(consumeResult.Message.Headers);
                var partitionKey = ReadPartitionKey(consumeResult.Message);
                var replyTo = ReadReplyTo(consumeResult.Message.Headers);
                var contentType = ReadContentType(consumeResult.Message.Headers);
                var delay = ReadDelay(consumeResult.Message.Headers);
                var handledCount = ReadHandledCount(consumeResult.Message.Headers);
                var subject = ReadSubject(consumeResult.Message.Headers);
                var dataSchema = ReadDataSchema(consumeResult.Message.Headers);
                var type = ReadType(consumeResult.Message.Headers);
                var source = ReadSource(consumeResult.Message.Headers);
                var traceParent = ReadTraceParent(consumeResult.Message.Headers);
                var traceState = ReadTraceState(consumeResult.Message.Headers);
                var baggage = ReadBaggage(consumeResult.Message.Headers);

                if (false == (topic.Success && messageId.Success && messageType.Success && timeStamp.Success))
                {
                    message = FailureMessage(topic, messageId);
                }
                else
                {
                    var messageHeader = new MessageHeader(
                        messageId: messageId.Result,
                        topic: topic.Result,
                        messageType.Result,
                        source: source.Result,
                        type: type.Result,
                        timeStamp: timeStamp.Success ? timeStamp.Result : DateTimeOffset.UtcNow,
                        correlationId: correlationId.Success ? correlationId.Result : "",
                        replyTo: replyTo.Success ? new RoutingKey(replyTo.Result) : RoutingKey.Empty,
                        contentType: contentType.Success ? contentType.Result : "plain/text",
                        partitionKey: partitionKey.Success ? partitionKey.Result : consumeResult.Message.Key,
                        handledCount: handledCount.Success ? handledCount.Result : 0,
                        dataSchema: dataSchema.Result,
                        subject: subject.Result,
                        delayed: delay.Success ? delay.Result : TimeSpan.Zero,
                        traceParent: traceParent.Result,
                        traceState: traceState.Result,
                        baggage: baggage.Result
                    );

                    message = new Message(messageHeader,
                        new MessageBody(consumeResult.Message.Value, messageHeader.ContentType ?? "plain/text"));

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
                Log.FailedToCreateMessageFromKafkaOffset(s_logger, e);
                message = FailureMessage(topic, messageId);
            }

            return message;
        }

        private Message FailureMessage(HeaderResult<RoutingKey> topic, HeaderResult<string> messageId)
        {
            var header = new MessageHeader(
                messageId.Success ? messageId.Result : string.Empty,
                topic.Success ? topic.Result : RoutingKey.Empty,
                MessageType.MT_UNACCEPTABLE);
            var message = new Message(header, new MessageBody(string.Empty));
            return message;
        }

        private static HeaderResult<string> ReadContentType(Headers headers)
        {
            var contentType = ReadHeader(headers, HeaderNames.CLOUD_EVENTS_DATA_CONTENT_TYPE, true);
            return !contentType.Success ? contentType : ReadHeader(headers, HeaderNames.CONTENT_TYPE);
        }

        private static HeaderResult<string> ReadCorrelationId(Headers headers)
        {
            return ReadHeader(headers, HeaderNames.CORRELATION_ID)
                .Map(correlationId =>
                {
                    if (string.IsNullOrEmpty(correlationId))
                    {
                        Log.NoCorrelationIdFoundInMessage(s_logger);
                        return new HeaderResult<string>(string.Empty, true);
                    }

                    return new HeaderResult<string>(correlationId, true);
                });
        }

        private static HeaderResult<TimeSpan> ReadDelay(Headers headers)
        {
            return ReadHeader(headers, HeaderNames.DELAYED_MILLISECONDS)
                .Map(s =>
                {
                    if (string.IsNullOrEmpty(s))
                    {
                        Log.NoDelayMillisecondsFoundInMessage(s_logger);
                        return new HeaderResult<TimeSpan>(TimeSpan.Zero, true);
                    }

                    if (int.TryParse(s, out int delayMilliseconds))
                    {
                        return new HeaderResult<TimeSpan>(TimeSpan.FromMilliseconds(delayMilliseconds), true);
                    }

                    Log.CouldNotParseMessageDelayMilliseconds(s_logger, s);
                    return new HeaderResult<TimeSpan>(TimeSpan.Zero, false);
                });
        }

        private static HeaderResult<int> ReadHandledCount(Headers headers)
        {
            return ReadHeader(headers, HeaderNames.HANDLED_COUNT)
                .Map(s =>
                {
                    if (string.IsNullOrEmpty(s))
                    {
                        Log.NoHandledCountFoundInMessage(s_logger);
                        return new HeaderResult<int>(0, true);
                    }

                    if (int.TryParse(s, out int handledCount))
                    {
                        return new HeaderResult<int>(handledCount, true);
                    }

                    Log.CouldNotParseMessageHandledCount(s_logger, s);
                    return new HeaderResult<int>(0, false);
                });
        }

        private static HeaderResult<string> ReadReplyTo(Headers headers)
        {
            return ReadHeader(headers, HeaderNames.REPLY_TO)
                .Map(s =>
                {
                    if (string.IsNullOrEmpty(s))
                    {
                        Log.NoReplyToFoundInMessage(s_logger);
                        return new HeaderResult<string>(string.Empty, true);
                    }

                    return new HeaderResult<string>(s, true);
                });
        }

        private static HeaderResult<DateTimeOffset> ReadTimeStamp(Headers headers)
        {
            if (headers.TryGetLastBytesIgnoreCase(HeaderNames.TIMESTAMP, out var lastHeader))
            {
                //Additional testing for a non unixtimestamp string
                if (DateTime.TryParse(lastHeader.FromByteArray(), DateTimeFormatInfo.InvariantInfo, DateTimeStyles.AssumeUniversal, out DateTime timestamp))
                {
                    return new HeaderResult<DateTimeOffset>(timestamp, true);
                }

                try
                {
                    return new HeaderResult<DateTimeOffset>(DateTimeOffset.FromUnixTimeMilliseconds(BitConverter.ToInt64(lastHeader, 0)).DateTime, true);
                }
                catch (Exception)
                {
                    return new HeaderResult<DateTimeOffset>(DateTimeOffset.UtcNow, true);
                }
            }

            return ReadHeader(headers, HeaderNames.CLOUD_EVENTS_TIME)
                .Map(x => DateTimeOffset.TryParse(x, out var timestamp)
                    ? new HeaderResult<DateTimeOffset>(timestamp, true)
                    : new HeaderResult<DateTimeOffset>(DateTimeOffset.UtcNow, true));
        }

        private static HeaderResult<MessageType> ReadMessageType(Headers headers)
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

        private static HeaderResult<RoutingKey> ReadTopic(string topic)
        {
            return new HeaderResult<RoutingKey>(new RoutingKey(topic), true);
        }

        private static HeaderResult<string> ReadMessageId(Headers headers)
        {
            var id = ReadHeader(headers, HeaderNames.CLOUD_EVENTS_ID, true)
                .Map(messageId =>
                {
                    if (string.IsNullOrEmpty(messageId))
                    {
                        return new HeaderResult<string>(string.Empty, false);
                    }

                    return new HeaderResult<string>(messageId, true);
                });
            if (id.Success)
            {
                return id;
            }

            var newMessageId = Guid.NewGuid().ToString();
            return ReadHeader(headers, HeaderNames.MESSAGE_ID)
                .Map(messageId =>
                {
                    if (string.IsNullOrEmpty(messageId))
                    {
                        Log.NoMessageIdFoundInMessage(s_logger, newMessageId);
                        return new HeaderResult<string>(newMessageId, true);
                    }

                    return new HeaderResult<string>(messageId, true);
                });
        }

        private static HeaderResult<string> ReadPartitionKey(Message<string, byte[]> message)
        {
            if (!string.IsNullOrEmpty(message.Key))
            {
                return new HeaderResult<string>(message.Key, true);
            }
            
            return ReadHeader(message.Headers, HeaderNames.PARTITIONKEY)
                .Map(s =>
                {
                    if (string.IsNullOrEmpty(s))
                    {
                        Log.NoPartitionKeyFoundInMessage(s_logger);
                        return new HeaderResult<string>(string.Empty, false);
                    }

                    return new HeaderResult<string>(s, true);
                });
        }

        private static HeaderResult<string> ReadSubject(Headers headers)
            => ReadHeader(headers, HeaderNames.CLOUD_EVENTS_SUBJECT);

        private static HeaderResult<string> ReadType(Headers headers)
            => ReadHeader(headers, HeaderNames.CLOUD_EVENTS_TYPE);

        private static HeaderResult<Uri> ReadDataSchema(Headers headers) =>
            ReadHeader(headers, HeaderNames.CLOUD_EVENTS_DATA_SCHEMA, true)
                .Map(x => Uri.TryCreate(x, UriKind.RelativeOrAbsolute, out var dataSchema)
                    ? new HeaderResult<Uri>(dataSchema, true)
                    : new HeaderResult<Uri>(null, false));

        private static HeaderResult<Uri> ReadSource(Headers headers) =>
            ReadHeader(headers, HeaderNames.CLOUD_EVENTS_SOURCE)
                .Map(x => Uri.TryCreate(x, UriKind.RelativeOrAbsolute, out var dataSchema)
                    ? new HeaderResult<Uri>(dataSchema, true)
                    : new HeaderResult<Uri>(new Uri("http://goparamore.io"), true));

        private static HeaderResult<string> ReadTraceParent(Headers headers)
        {
            return ReadHeader(headers, HeaderNames.CLOUD_EVENTS_TRACE_PARENT)
                .Map(s =>
                {
                    if (string.IsNullOrEmpty(s))
                    {
                        return new HeaderResult<string>(string.Empty, true);
                    }

                    return new HeaderResult<string>(s, true);
                });
        }

        private static HeaderResult<string> ReadTraceState(Headers headers)
        {
            return ReadHeader(headers, HeaderNames.CLOUD_EVENTS_TRACE_STATE)
                .Map(s =>
                {
                    if (string.IsNullOrEmpty(s))
                    {
                        return new HeaderResult<string>(string.Empty, true);
                    }

                    return new HeaderResult<string>(s, true);
                });
        }

        private static HeaderResult<string> ReadBaggage(Headers headers)
        {
            return ReadHeader(headers, HeaderNames.W3C_BAGGAGE)
                .Map(s =>
                {
                    if (string.IsNullOrEmpty(s))
                    {
                        return new HeaderResult<string>(string.Empty, true);
                    }

                    return new HeaderResult<string>(s, true);
                });
        }

        private static HeaderResult<string> ReadHeader(Headers headers, string key, bool dieOnMissing = false)
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
                    Log.FailedToReadTheValueOfHeader(s_logger, e, key, firstTwentyBytes);
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

        private static partial class Log
        {
            [LoggerMessage(LogLevel.Warning, "Failed to create message from Kafka offset")]
            public static partial void FailedToCreateMessageFromKafkaOffset(ILogger logger, Exception exception);

            [LoggerMessage(LogLevel.Debug, "No correlation id found in message")]
            public static partial void NoCorrelationIdFoundInMessage(ILogger logger);

            [LoggerMessage(LogLevel.Debug, "No delay milliseconds found in message")]
            public static partial void NoDelayMillisecondsFoundInMessage(ILogger logger);

            [LoggerMessage(LogLevel.Debug, "Could not parse message delayMilliseconds: {DelayMillisecondsValue}")]
            public static partial void CouldNotParseMessageDelayMilliseconds(ILogger logger, string delayMillisecondsValue);

            [LoggerMessage(LogLevel.Debug, "No handled count found in message")]
            public static partial void NoHandledCountFoundInMessage(ILogger logger);

            [LoggerMessage(LogLevel.Debug, "Could not parse message handled count: {HandledCountValue}")]
            public static partial void CouldNotParseMessageHandledCount(ILogger logger, string handledCountValue);

            [LoggerMessage(LogLevel.Debug, "No reply to found in message")]
            public static partial void NoReplyToFoundInMessage(ILogger logger);

            [LoggerMessage(LogLevel.Debug, "No message id found in message MessageId, new message id is {NewMessageId}")]
            public static partial void NoMessageIdFoundInMessage(ILogger logger, string newMessageId);

            [LoggerMessage(LogLevel.Debug, "No partition key found in message")]
            public static partial void NoPartitionKeyFoundInMessage(ILogger logger);

            [LoggerMessage(LogLevel.Warning, "Failed to read the value of header {Topic} as UTF-8, first 20 byes follow: \n\t{FirstTwentyBytes}")]
            public static partial void FailedToReadTheValueOfHeader(ILogger logger, Exception exception, string topic, string firstTwentyBytes);
        }
    }
}
