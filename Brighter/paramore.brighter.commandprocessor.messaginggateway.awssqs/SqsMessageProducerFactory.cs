using paramore.brighter.commandprocessor.Logging;

namespace paramore.brighter.commandprocessor.messaginggateway.awssqs
{
    public class SqsMessageProducerFactory : IAmAMessageProducerFactory
    {
        public SqsMessageProducerFactory() {}

        public IAmAMessageProducer Create()
        {
            return new SqsMessageProducer();
        }
    }
}