using paramore.brighter.commandprocessor;
using SimpleInjector;

namespace ProtoBufSender.Configuration
{
    static class MessageProcessorConfig
    {
        public static CommandProcessor Register(Container container)
        {
            var messageHandlerConfig = MessageHandlerConfig.Register(container);

            var rmqMessagePublisherConfig = RabbitMqMessagePublisherConfig.Register(container);

            var messageProcessor = CommandProcessorBuilder.With()
                .Handlers(messageHandlerConfig)
                .DefaultPolicy()
                .TaskQueues(rmqMessagePublisherConfig) //enables publishing commands and events to rabbit mq
                .RequestContextFactory(new InMemoryRequestContextFactory())
                .Build();

            container.RegisterSingleton<IAmACommandProcessor>(messageProcessor);

            return messageProcessor;
        }
    }
}
