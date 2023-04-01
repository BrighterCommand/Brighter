using System;
using System.Threading.Tasks;
using Confluent.Kafka;

namespace Paramore.Brighter.MessagingGateway.Kafka
{
    internal class KafkaMessagePublisher
    {
        private readonly IProducer<string, byte[]> _producer;

        private readonly IKafkaMessageHeaderBuilder _headerBuilder;

        public KafkaMessagePublisher(IProducer<string, byte[]> producer,
            IKafkaMessageHeaderBuilder headerBuilder)
        {
            _producer = producer;
            _headerBuilder = headerBuilder;
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

        private Message<string, byte[]> BuildMessage(Message message)
        {
            var headers = _headerBuilder.Build(message);

            var kafkaMessage = new Message<string, byte[]>
            {
                Headers = headers,
                Key = message.Header.PartitionKey,
                Value = message.Body.Bytes
            };

            return kafkaMessage;
        }
    }
}
