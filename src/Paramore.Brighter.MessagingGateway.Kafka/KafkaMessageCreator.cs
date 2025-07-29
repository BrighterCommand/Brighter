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
using System.Net.Mime;
using System.Text;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Observability;

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

        private sealed class MessageHeaderResults
        {
            public required HeaderResult<RoutingKey> Topic { get; set; }
            public required HeaderResult<Id?> MessageId { get; set; }
            public required HeaderResult<MessageType> MessageType { get; set; }
            public required HeaderResult<DateTimeOffset> TimeStamp { get; set; }
            public required HeaderResult<Id?> CorrelationId { get; set; }
            public required HeaderResult<PartitionKey?> PartitionKey { get; set; }
            public required HeaderResult<RoutingKey?> ReplyTo { get; set; }
            public required HeaderResult<ContentType?> ContentType { get; set; }
            public required HeaderResult<TimeSpan> Delay { get; set; }
            public required HeaderResult<int> HandledCount { get; set; }
            public required HeaderResult<string?> Subject { get; set; }
            public required HeaderResult<Uri?> DataSchema { get; set; }
            public required HeaderResult<string?> Type { get; set; }
            public required HeaderResult<Uri?> Source { get; set; }
            public required HeaderResult<TraceParent?> TraceParent { get; set; }
            public required HeaderResult<TraceState?> TraceState { get; set; }
            public required HeaderResult<Baggage?> Baggage { get; set; } 
        }

        public Message CreateMessage(ConsumeResult<string, byte[]> consumeResult)
        {
            try
            {
                var headerResults = KafkaMessageCreator.ReadAllHeaders(consumeResult);
                return CreateMessageFromHeaders(headerResults, consumeResult);
            }
            catch (Exception e)
            {
                Log.FailedToCreateMessageFromKafkaOffset(s_logger, e);
                return Message.FailureMessage(RoutingKey.Empty, Id.Empty);
            }
        }

        private static MessageHeaderResults ReadAllHeaders(ConsumeResult<string, byte[]> consumeResult)
        {
            var result = new MessageHeaderResults
            {
                Topic = ReadTopic(consumeResult.Topic),
                MessageId = ReadMessageId(consumeResult.Message.Headers),
                TimeStamp = ReadTimeStamp(consumeResult.Message.Headers),
                MessageType = ReadMessageType(consumeResult.Message.Headers),
                CorrelationId = ReadCorrelationId(consumeResult.Message.Headers),
                PartitionKey = ReadPartitionKey(consumeResult.Message),
                ReplyTo = ReadReplyTo(consumeResult.Message.Headers),
                ContentType = ReadContentType(consumeResult.Message.Headers),
                Delay = ReadDelay(consumeResult.Message.Headers),
                HandledCount = ReadHandledCount(consumeResult.Message.Headers),
                Subject = ReadSubject(consumeResult.Message.Headers),
                DataSchema = ReadDataSchema(consumeResult.Message.Headers),
                Type = ReadType(consumeResult.Message.Headers),
                Source = ReadSource(consumeResult.Message.Headers),
                TraceParent = ReadTraceParent(consumeResult.Message.Headers),
                TraceState = ReadTraceState(consumeResult.Message.Headers),
                Baggage = ReadBaggage(consumeResult.Message.Headers)
            };

            return result;
        }

        private Message CreateMessageFromHeaders(MessageHeaderResults headers, ConsumeResult<string, byte[]> consumeResult)
        {
            if (!(headers.Topic.Success && headers.MessageId.Success && headers.MessageType.Success && headers.TimeStamp.Success))
            {
                return Message.FailureMessage(headers.Topic.Result, headers.MessageId.Result);
            }

            var message = SuccessMessage(headers, consumeResult);
            AddPartitionOffset(message, consumeResult);
            AddCustomHeaders(message, consumeResult.Message.Headers);
            
            return message;
        }

        private static Message SuccessMessage(MessageHeaderResults headers, ConsumeResult<string, byte[]> consumeResult)
        {
            var messageHeader = new MessageHeader(
                messageId: (headers.MessageId.Success ? headers.MessageId.Result : Id.Empty)!,
                topic: headers.Topic.Result,
                headers.MessageType.Result,
                source: headers.Source.Result,
                type: headers.Type.Result,
                timeStamp: headers.TimeStamp.Success ? headers.TimeStamp.Result : DateTimeOffset.UtcNow,
                correlationId: headers.CorrelationId.Success ? headers.CorrelationId.Result : "",
                replyTo: headers.ReplyTo.Success ? headers.ReplyTo.Result : RoutingKey.Empty,
                contentType: headers.ContentType.Success ? headers.ContentType.Result : new ContentType(MediaTypeNames.Text.Plain),
                partitionKey: headers.PartitionKey.Success ? headers.PartitionKey.Result : new PartitionKey(consumeResult.Message.Key),
                handledCount: headers.HandledCount.Success ? headers.HandledCount.Result : 0,
                dataSchema: headers.DataSchema.Result,
                subject: headers.Subject.Result,
                delayed: headers.Delay.Success ? headers.Delay.Result : TimeSpan.Zero,
                traceParent: headers.TraceParent.Result,
                traceState: headers.TraceState.Result,
                baggage: headers.Baggage.Result
            );

            return new Message(messageHeader, new MessageBody(consumeResult.Message.Value, messageHeader.ContentType ?? new ContentType(MediaTypeNames.Text.Plain)));
        }

        private void AddPartitionOffset(Message message, ConsumeResult<string, byte[]> consumeResult)
        {
            if (!message.Header.Bag.ContainsKey(HeaderNames.PARTITION_OFFSET))
                message.Header.Bag.Add(HeaderNames.PARTITION_OFFSET, consumeResult.TopicPartitionOffset);
        }

        private void AddCustomHeaders(Message message, Headers headers)
        {
            headers.Each(header => ReadBagEntry(header, message));
        }

        private static HeaderResult<ContentType?> ReadContentType(Headers headers)
        {
            var contentType = ReadHeader(headers, HeaderNames.CLOUD_EVENTS_DATA_CONTENT_TYPE, true);
            
            if (contentType.Success && !string.IsNullOrEmpty(contentType.Result))
                return new HeaderResult<ContentType?>( new ContentType(contentType.Result!), true);

            contentType = ReadHeader(headers, HeaderNames.CONTENT_TYPE);
            if (contentType.Success && !string.IsNullOrEmpty(contentType.Result))
                return new HeaderResult<ContentType?>(new ContentType(contentType.Result!), true);

            return new HeaderResult<ContentType?>(null, false);
        }

        private static HeaderResult<Id?> ReadCorrelationId(Headers headers)
        {
            return ReadHeader(headers, HeaderNames.CORRELATION_ID)
                .Map(correlationId =>
                {
                    if (string.IsNullOrEmpty(correlationId))
                    {
                        Log.NoCorrelationIdFoundInMessage(s_logger);
                        return new HeaderResult<Id?>(Id.Empty, true);
                    }

                    return new HeaderResult<Id?>(new Id(correlationId!), true);
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

                    Log.CouldNotParseMessageDelayMilliseconds(s_logger, s!);
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

                    Log.CouldNotParseMessageHandledCount(s_logger, s!);
                    return new HeaderResult<int>(0, false);
                });
        }

        private static HeaderResult<RoutingKey?> ReadReplyTo(Headers headers)
        {
            return ReadHeader(headers, HeaderNames.REPLY_TO)
                .Map(s =>
                {
                    if (string.IsNullOrEmpty(s))
                    {
                        Log.NoReplyToFoundInMessage(s_logger);
                        return new HeaderResult<RoutingKey?>(RoutingKey.Empty, true);
                    }

                    return new HeaderResult<RoutingKey?>(new RoutingKey(s!), true);
                });
        }

        private static HeaderResult<DateTimeOffset> ReadTimeStamp(Headers headers)
        {
            if (headers.TryGetLastBytesIgnoreCase(HeaderNames.TIMESTAMP, out var lastHeader))
            {
                //Additional testing for a non unixtimestamp string
                if (DateTime.TryParse(lastHeader!.FromByteArray(), DateTimeFormatInfo.InvariantInfo, DateTimeStyles.AssumeUniversal, out DateTime timestamp))
                {
                    return new HeaderResult<DateTimeOffset>(timestamp, true);
                }

                try
                {
                    return new HeaderResult<DateTimeOffset>(DateTimeOffset.FromUnixTimeMilliseconds(BitConverter.ToInt64(lastHeader!, 0)).DateTime, true);
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

        private static HeaderResult<Id?> ReadMessageId(Headers headers)
        {
            var id = ReadHeader(headers, HeaderNames.CLOUD_EVENTS_ID, true)
                .Map(messageId => new HeaderResult<Id?>(string.IsNullOrEmpty(messageId) ? Id.Random : Id.Create(messageId), true));
            
            if (id.Success)
            {
                return id;
            }

            var newMessageId = Uuid.NewAsString();
            return ReadHeader(headers, HeaderNames.MESSAGE_ID)
                .Map(messageId =>
                {
                    if (string.IsNullOrEmpty(messageId))
                    {
                        Log.NoMessageIdFoundInMessage(s_logger, newMessageId);
                        return new HeaderResult<Id?>(Id.Random, true);
                    }

                    return new HeaderResult<Id?>(new Id(messageId!), true);
                });
        }

        private static HeaderResult<PartitionKey?> ReadPartitionKey(Message<string, byte[]> message)
        {

            var pKey = ReadHeader(message.Headers, HeaderNames.PARTITIONKEY)
                .Map(s =>
                {
                    if (string.IsNullOrEmpty(s))
                    {
                        Log.NoPartitionKeyFoundInMessage(s_logger);
                        return new HeaderResult<PartitionKey?>(PartitionKey.Empty, false);
                    }

                    return new HeaderResult<PartitionKey?>(new PartitionKey(s!), true);
                });

            //if we set the partition key in the message bag, we assume it is a Brighter message, and we use that
            if (pKey.Success)
            {
                return pKey;
            }
           
            //if we have no partition key header, but we have a message key, we assume it is not a Brighter message,
            //and we use the message key as the partition key
            if (!string.IsNullOrEmpty(message.Key))
            {
                return new HeaderResult<PartitionKey?>(message.Key, true);
            }
            
            //if we have no partition key header, and no message key, we return empty
            return new HeaderResult<PartitionKey?>(PartitionKey.Empty, false); 
        }

        private static HeaderResult<string?> ReadSubject(Headers headers)
            => ReadHeader(headers, HeaderNames.CLOUD_EVENTS_SUBJECT);

        private static HeaderResult<string?> ReadType(Headers headers)
            => ReadHeader(headers, HeaderNames.CLOUD_EVENTS_TYPE);

        private static HeaderResult<Uri?> ReadDataSchema(Headers headers) =>
            ReadHeader(headers, HeaderNames.CLOUD_EVENTS_DATA_SCHEMA, true)
                .Map(x => Uri.TryCreate(x, UriKind.RelativeOrAbsolute, out var dataSchema)
                    ? new HeaderResult<Uri?>(dataSchema, true)
                    : new HeaderResult<Uri?>(null, false));

        private static HeaderResult<Uri?> ReadSource(Headers headers) =>
            ReadHeader(headers, HeaderNames.CLOUD_EVENTS_SOURCE)
                .Map(x => Uri.TryCreate(x, UriKind.RelativeOrAbsolute, out var dataSchema)
                    ? new HeaderResult<Uri?>(dataSchema, true)
                    : new HeaderResult<Uri?>(new Uri("http://goparamore.io"), true));

        private static HeaderResult<TraceParent?> ReadTraceParent(Headers headers)
        {
            return ReadHeader(headers, HeaderNames.CLOUD_EVENTS_TRACE_PARENT)
                .Map(s =>
                {
                    if (string.IsNullOrEmpty(s))
                    {
                        return new HeaderResult<TraceParent?>(TraceParent.Empty, true);
                    }

                    return new HeaderResult<TraceParent?>(new TraceParent(s!), true);
                });
        }

        private static HeaderResult<TraceState?> ReadTraceState(Headers headers)
        {
            return ReadHeader(headers, HeaderNames.CLOUD_EVENTS_TRACE_STATE)
                .Map(s =>
                {
                    if (string.IsNullOrEmpty(s))
                    {
                        return new HeaderResult<TraceState?>(TraceState.Empty, true);
                    }

                    return new HeaderResult<TraceState?>(new TraceState(s!), true);
                });
        }

        private static HeaderResult<Baggage?> ReadBaggage(Headers headers)
        {
            return ReadHeader(headers, HeaderNames.W3C_BAGGAGE)
                .Map(s =>
                {
                    var baggage = new Baggage();
                    if (string.IsNullOrEmpty(s))
                    {
                        return new HeaderResult<Baggage?>(baggage, true);
                    }

                    baggage.LoadBaggage(s!);
                    return new HeaderResult<Baggage?>(baggage, true);
                });
        }

        private static HeaderResult<string?> ReadHeader(Headers headers, string key, bool dieOnMissing = false)
        {
            if (headers.TryGetLastBytesIgnoreCase(key, out byte[]? lastHeader))
            {
                try
                {
                    var val = lastHeader.FromByteArray();
                    return new HeaderResult<string?>(val, true);
                }
                catch (Exception e)
                {
                    var firstTwentyBytes = BitConverter.ToString(lastHeader!.Take(20).ToArray());
                    Log.FailedToReadTheValueOfHeader(s_logger, e, key, firstTwentyBytes);
                    return new HeaderResult<string?>(null, false);
                }
            }

            return new HeaderResult<string?>(string.Empty, !dieOnMissing);
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
