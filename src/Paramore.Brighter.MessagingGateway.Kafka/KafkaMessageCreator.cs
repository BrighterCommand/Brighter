using System;
using System.Linq;
using System.Text;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.MessagingGateway.Kafka
{
    internal class KafkaMessageCreator
    {
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<KafkaMessageCreator>();

        public Message CreateMessage(ConsumeResult<string, byte[]> consumeResult)
        {
            var headers = consumeResult.Message.Headers;
            var topic = HeaderResult<string>.Empty();
            var messageId = HeaderResult<Guid>.Empty();
            var timeStamp = HeaderResult<DateTime>.Empty();
            var messageType = HeaderResult<MessageType>.Empty();
            var correlationId = HeaderResult<Guid>.Empty();
            var partitionKey = HeaderResult<string>.Empty();
            var replyTo = HeaderResult<string>.Empty();
            var contentType = HeaderResult<string>.Empty();

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

                    if (partitionKey.Success)
                        messageHeader.PartitionKey = partitionKey.Result;
                    
                    if (contentType.Success)
                        messageHeader.ContentType =contentType.Result;

                    if (replyTo.Success)
                        messageHeader.ReplyTo = replyTo.Result;

                    message = new Message(messageHeader, new MessageBody(consumeResult.Message.Value, messageHeader.ContentType));

                    headers.Each(header => message.Header.Bag.Add(header.Key, ParseHeaderValue(header)));
                    
                    if (!message.Header.Bag.ContainsKey(HeaderNames.PARTITION_OFFSET))
                        message.Header.Bag.Add(HeaderNames.PARTITION_OFFSET, consumeResult.TopicPartitionOffset);
                }
            }
            catch (Exception e)
            {
                s_logger.LogWarning(e, "Failed to create message from Kafka offset");
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
        
        
        private HeaderResult<string> ReadContentType(Headers headers)
        {
           return ReadHeader(headers, HeaderNames.CONTENT_TYPE,false);
        }

        private HeaderResult<Guid> ReadCorrelationId(Headers headers)
        {
            if (headers.TryGetLastBytes(HeaderNames.CORRELATION_ID, out byte[] lastHeader))
            {
                var correlationValue = Encoding.UTF8.GetString(lastHeader);
                if (Guid.TryParse(correlationValue, out Guid correlationId))
                {
                    return new HeaderResult<Guid>(correlationId, true);
                }
                else
                {
                    s_logger.LogDebug("Could not parse message correlation id: {CorrelationValue}", correlationValue);
                    return new HeaderResult<Guid>(Guid.Empty, false);
                }
            }

            return new HeaderResult<Guid>(Guid.Empty, false);
        }

        private HeaderResult<string> ReadReplyTo(Headers headers)
        {
            if (headers.TryGetLastBytes(HeaderNames.REPLY_TO, out byte[] lastHeader))
            {
                var replyToValue = Encoding.UTF8.GetString(lastHeader);
                return new HeaderResult<string>(replyToValue, true);
            }
        
            return new HeaderResult<string>(string.Empty, false);
        }

        private HeaderResult<DateTime> ReadTimeStamp(Headers headers)
        {
            if (headers.TryGetLastBytes(HeaderNames.TIMESTAMP, out byte[] lastHeader))
            {
                return new HeaderResult<DateTime>(UnixTimestamp.DateTimeFromUnixTimestampSeconds(BitConverter.ToInt64(lastHeader, 0)), true);
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

        private HeaderResult<Guid> ReadMessageId(Headers headers)
        {
            var newMessageId = Guid.NewGuid();

            return ReadHeader(headers, HeaderNames.MESSAGE_ID)
                .Map(s =>
                {
                    if (string.IsNullOrEmpty(s))
                    {
                        s_logger.LogDebug("No message id found in message MessageId, new message id is {NewMessageId}", newMessageId);
                        return new HeaderResult<Guid>(newMessageId, true);
                    }

                    if (Guid.TryParse(s, out Guid messageId))
                    {
                        return new HeaderResult<Guid>(messageId, true);
                    }

                    s_logger.LogDebug("Could not parse message MessageId, new message id is {Id}", Guid.Empty);
                    return new HeaderResult<Guid>(Guid.Empty, false);
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
                        return new HeaderResult<string>(string.Empty, true);
                    }

                    return new HeaderResult<string>(s, true);
                });
        }


        private static object ParseHeaderValue(object value)
        {
            return value is byte[] bytes ? Encoding.UTF8.GetString(bytes) : value;
        }

        private HeaderResult<string> ReadHeader(Headers headers, string key, bool dieOnMissing = false)
        {
            if (headers.TryGetLastBytes(key, out byte[] lastHeader))
            {
                try
                {
                    var val = lastHeader.FromByteArray();
                    return new HeaderResult<string>(val, true);
                }
                catch (Exception e)
                {
                    var firstTwentyBytes = BitConverter.ToString(lastHeader.Take(20).ToArray());
                    s_logger.LogWarning(e, "Failed to read the value of header {Topic} as UTF-8, first 20 byes follow: \n\t{1}", key, firstTwentyBytes);
                    return new HeaderResult<string>(null, false);
                }
            }
            else
            {
                return new HeaderResult<string>(string.Empty, !dieOnMissing);
            }
        }
    }
}
