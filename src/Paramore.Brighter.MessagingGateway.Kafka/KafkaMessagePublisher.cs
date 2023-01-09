using System;
using System.Linq;
using System.Threading.Tasks;
using Confluent.Kafka;
using Paramore.Brighter.Extensions;

namespace Paramore.Brighter.MessagingGateway.Kafka
{
    internal class KafkaMessagePublisher
    {
        private readonly IProducer<string, byte[]> _producer;
        
        private static readonly string[] s_headersToReset;
        
        public KafkaMessagePublisher(IProducer<string, byte[]> producer)
        {
            _producer = producer;
        }

        static KafkaMessagePublisher()
        {
            s_headersToReset = new[] {
                HeaderNames.MESSAGE_TYPE,
                HeaderNames.TOPIC,
                HeaderNames.CORRELATION_ID,
                HeaderNames.TIMESTAMP
            };
        }

        public void PublishMessage(Message message, Action<DeliveryReport<string, byte[]>> deliveryReport)
        {
            var kafkaMessage = BuildMessage(message);
            
            _producer.Produce(message.Header.Topic, kafkaMessage, deliveryReport);
        }

        public async Task PublishMessageAsync(Message message, Action<DeliveryResult<string, byte[]>> deliveryReport)
        {
            var kafkaMessage = BuildMessage(message);
            
            var deliveryResult = await _producer.ProduceAsync(message.Header.Topic, kafkaMessage);
            deliveryReport(deliveryResult);
        }

        private static Message<string, byte[]> BuildMessage(Message message)
        {
            var headers = new Headers()
            {
                new Header(HeaderNames.MESSAGE_TYPE, message.Header.MessageType.ToString().ToByteArray()),
                new Header(HeaderNames.TOPIC, message.Header.Topic.ToByteArray()),
                new Header(HeaderNames.MESSAGE_ID, message.Header.Id.ToString().ToByteArray()),
                new Header(HeaderNames.TIMESTAMP, BitConverter.GetBytes(UnixTimestamp.GetCurrentUnixTimestampSeconds()))
            };

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
                        default:
                            headers.Add(header.Key, header.Value.ToString().ToByteArray());
                            break;
                    }
                }
            });

            var kafkaMessage = new Message<string, byte[]>()
            {
                Headers = headers,
                Key = message.Header.PartitionKey,
                Value = message.Body.Bytes
            };
            return kafkaMessage;
        }
    }
}
