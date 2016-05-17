using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor.messaginggateway.rmq;

namespace ProtoBufReceiver.Configuration
{
    static class RabbitMqInputChannelConfig
    {
        public static InputChannelFactory Register(ILog logger)
        {
            var rmqMessageConsumerFactory = new RmqMessageConsumerFactory(logger);

            var rmqMessageProducerFactory = new RmqMessageProducerFactory(logger);

            return new InputChannelFactory(rmqMessageConsumerFactory, rmqMessageProducerFactory);
        }
    }
}
