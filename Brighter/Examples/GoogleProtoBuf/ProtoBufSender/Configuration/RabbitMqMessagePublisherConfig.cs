using System;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.messaginggateway.rmq;
using SimpleInjector;

namespace ProtoBufSender.Configuration
{
    static class RabbitMqMessagePublisherConfig
    {
        public static MessagingConfiguration Register(Container container)
        {
            var messageMapperRegistry = MessageMapperConfig.Register(container); //Maps events and commands to messages (and vice versa)

            var rmqMessageProducer = new RmqMessageProducer();

            var nullMessageStore = new NullMessageStore(); //Not using this feature in this example

            return new MessagingConfiguration(nullMessageStore, rmqMessageProducer, messageMapperRegistry);
        }
    }

    class NullMessageStore : IAmAMessageStore<Message>
    {
        public void Add(Message message, int messageStoreTimeout = -1)
        {
        }

        public Message Get(Guid messageId, int messageStoreTimeout = -1)
        {
            return null;
        }
    }
}
