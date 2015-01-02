using Common.Logging;

namespace paramore.brighter.commandprocessor.messaginggateway.rmq
{
    public class RmqMessageConsumerFactory : IAmAMessageConsumerFactory
    {
        private readonly ILog _logger;

        public RmqMessageConsumerFactory(ILog logger)
        {
            _logger = logger;
        }

        public IAmAMessageConsumer Create()
        {
            return new RmqMessageConsumer(_logger);
        }
    }
}