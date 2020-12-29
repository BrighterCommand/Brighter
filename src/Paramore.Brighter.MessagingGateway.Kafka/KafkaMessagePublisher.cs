using System;
using System.Linq;
using System.Threading.Tasks;
using Confluent.Kafka;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.MessagingGateway.Kafka
{
    internal class KafkaMessagePublisher
    {
        private static readonly Lazy<ILog> _logger = new Lazy<ILog>(LogProvider.For<KafkaMessagePublisher>);
        private readonly IProducer<string, string> _producer;
        private static readonly string[] _headersToReset =
        {
            HeaderNames.MESSAGE_TYPE,
            HeaderNames.TOPIC,
            HeaderNames.CORRELATION_ID,
            HeaderNames.TIMESTAMP
        };


        public KafkaMessagePublisher(IProducer<string, string> producer)
        {
            _producer = producer;
        }

        public async Task PublishMessageAsync(Message message)
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
            
            message.Header.Bag.Each((header) => 
            {
                 if (!_headersToReset.Any(htr => htr.Equals(header.Key)))
                 {
                     switch (header.Value)
                     {
                         case string stringValue:
                            headers.Add(header.Key, stringValue.ToByteArray());
                            break;
                         case int intValue:
                             headers.Add(header.Key, BitConverter.GetBytes(intValue));
                             break;
                     }
                 }
            });

            var kafkaMessage = new Message<string, string>()
            {   
                Headers = headers,
                Key = message.Header.PartitionKey,
                Value = message.Body.Value
            };
            var deliveryResult = await _producer.ProduceAsync(message.Header.Topic, kafkaMessage);
            if (deliveryResult.Status != PersistenceStatus.Persisted)
            {
                throw new ChannelFailureException($"The message was not persisted to Kafka");
            }
        }
    }
}
