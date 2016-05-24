using paramore.brighter.commandprocessor;
using SimpleInjector;

namespace ProtoBufSender.Configuration
{
    static class BrighterConfig
    {
        public static CommandProcessor Register(Container container)
        {
            Log4NetConfig.Register(container);

            return MessageProcessorConfig.Register(container);
        }
    }
}
