using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;
using SimpleInjector;

namespace ProtoBufReceiver.Configuration
{
    internal class MessageProcessorConfig
    {
        public static CommandProcessor Register(Container container, ILog logger)
        {
            var messageHandlerConfig = MessageHandlerConfig.Register(container);

            var messageProcessor = CommandProcessorBuilder.With()
                .Handlers(messageHandlerConfig)
                .DefaultPolicy()
                //.Logger(logger)
                .NoTaskQueues() //This example does not need to publish messages
                .RequestContextFactory(new InMemoryRequestContextFactory())
                .Build();

            container.RegisterSingleton<IAmACommandProcessor>(messageProcessor);

            return messageProcessor;
        }
    }
}
