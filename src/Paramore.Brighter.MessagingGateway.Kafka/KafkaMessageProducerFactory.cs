using System;

namespace Paramore.Brighter.MessagingGateway.Kafka
{
    public class KafkaMessageProducerFactory : IAmAMessageProducerFactory
    {
        public IAmAMessageProducer Create()
        {
            return new KafkaMessageProducer();
        }
    }
}
