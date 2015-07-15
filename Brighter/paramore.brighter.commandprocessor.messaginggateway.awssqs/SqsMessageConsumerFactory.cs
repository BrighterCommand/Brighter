using paramore.brighter.commandprocessor.Logging;

namespace paramore.brighter.commandprocessor.messaginggateway.awssqs
{
    public class SqsMessageConsumerFactory : IAmAMessageConsumerFactory
    {
        private readonly ILog _logger;

        public SqsMessageConsumerFactory(ILog logger)
        {
            _logger = logger;
        }

        public IAmAMessageConsumer Create(string channelName, string routingKey, bool isDurable)
        {
            return new SqsMessageConsumer(channelName, _logger);
        }
    }
}