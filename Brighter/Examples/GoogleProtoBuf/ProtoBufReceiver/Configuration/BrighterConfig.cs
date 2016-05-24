using paramore.brighter.serviceactivator;
using SimpleInjector;

namespace ProtoBufReceiver.Configuration
{
    internal class BrighterConfig
    {
        public static IDispatcher Register(Container container)
        {
            var logger = Log4NetConfig.Register(container);

            var commandProcessor = MessageProcessorConfig.Register(container, logger);

            var dispatcher = MessageDispatcherConfig.Register(container, logger, commandProcessor);

            return dispatcher;
        }
    }
}
