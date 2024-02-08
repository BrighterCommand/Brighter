using System;
using System.Linq;
using Confluent.Kafka;
using Paramore.Brighter.Extensions;

namespace Paramore.Brighter.MessagingGateway.Kafka
{
    public class KafkaDefaultMessageHeaderBuilder : IKafkaMessageHeaderBuilder
    {
        private static readonly string[] s_headersToReset;

        static KafkaDefaultMessageHeaderBuilder()
        {
            s_headersToReset = new[] {
                HeaderNames.MESSAGE_TYPE,
                HeaderNames.TOPIC,
                HeaderNames.CORRELATION_ID,
                HeaderNames.TIMESTAMP
            };
        }

        public static KafkaDefaultMessageHeaderBuilder Instance => new KafkaDefaultMessageHeaderBuilder();

        public Headers Build(Message message)
        {
            var headers = new Headers
            {
                new Header(HeaderNames.MESSAGE_TYPE, message.Header.MessageType.ToString().ToByteArray()),
                new Header(HeaderNames.TOPIC, message.Header.Topic.ToByteArray()),
                new Header(HeaderNames.MESSAGE_ID, message.Header.Id.ToString().ToByteArray()),
            };

            if (message.Header.TimeStamp != default)
                headers.Add(HeaderNames.TIMESTAMP, BitConverter.GetBytes(new DateTimeOffset(message.Header.TimeStamp).ToUnixTimeMilliseconds()));
            else
                headers.Add(HeaderNames.TIMESTAMP, BitConverter.GetBytes(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
            
            if (message.Header.CorrelationId != Guid.Empty)
                headers.Add(HeaderNames.CORRELATION_ID, message.Header.CorrelationId.ToString().ToByteArray());

            if (!string.IsNullOrEmpty(message.Header.PartitionKey))
                headers.Add(HeaderNames.PARTITIONKEY, message.Header.PartitionKey.ToByteArray());

            if (!string.IsNullOrEmpty(message.Header.ContentType))
                headers.Add(HeaderNames.CONTENT_TYPE, message.Header.ContentType.ToByteArray());

            if (!string.IsNullOrEmpty(message.Header.ReplyTo))
                headers.Add(HeaderNames.REPLY_TO, message.Header.ReplyTo.ToByteArray());
            
            message.Header.Bag.Each((header) =>
            {
                if (!s_headersToReset.Any(htr => htr.Equals(header.Key)))
                {
                    switch (header.Value)
                    {
                        case string stringValue:
                            headers.Add(header.Key, stringValue.ToByteArray());
                            break;
                        case int intValue:
                            headers.Add(header.Key, BitConverter.GetBytes(intValue));
                            break;
                        case Guid guidValue:
                            headers.Add(header.Key, guidValue.ToString().ToByteArray());
                            break;
                        case byte[] byteArray:
                            headers.Add(header.Key, byteArray);
                            break;
                        case double doubleValue:
                            headers.Add(header.Key, BitConverter.GetBytes(doubleValue));
                            break;
                        case DateTime dateTimeValue:
                            headers.Add(header.Key, dateTimeValue.ToString().ToByteArray());
                            break;
                        default:
                            headers.Add(header.Key, header.Value.ToString().ToByteArray());
                            break;
                    }
                }
            });

            return headers;
        }
    }
}
