using SimpleInjector;
using paramore.brighter.commandprocessor;
using paramore.brighter.serviceactivator;
using paramore.brighter.commandprocessor.Logging;

namespace ProtoBufReceiver.Configuration
{
    internal class MessageDispatcherConfig
    {
        public static Dispatcher Register(Container container, ILog logger, CommandProcessor commandProcessor)
        {
            //Configure the dispatcher, which reads messages off queues and maps them to commands/events

            var messageMapperRegistry = MessageMapperConfig.Register(container);

            var rmqInputChannelFactory = RabbitMqInputChannelConfig.Register(logger);

            var dispatcher = DispatchBuilder.With()
                //                .Logger(logger)
                .CommandProcessor(commandProcessor)
                .MessageMappers(messageMapperRegistry) //map task queue messages to commands/events
                .ChannelFactory(rmqInputChannelFactory) //reads messages from rabbit mq
                .ConnectionsFromConfiguration() //get queue routing rules from the app.config file
                .Build();

            return dispatcher;
        }

    }
}
