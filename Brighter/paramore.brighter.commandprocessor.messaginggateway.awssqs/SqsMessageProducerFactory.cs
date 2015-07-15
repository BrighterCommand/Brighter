using paramore.brighter.commandprocessor.Logging;

namespace paramore.brighter.commandprocessor.messaginggateway.awssqs
{
    public class SqsMessageProducerFactory : IAmAMessageProducerFactory
    {
        private readonly ILog _logger;

        public SqsMessageProducerFactory(ILog logger)
        {
            _logger = logger;
        }

        public IAmAMessageProducer Create()
        {
            return new SqsMessageProducer(_logger);
        }
    }
}