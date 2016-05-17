using log4net.Config;
using paramore.brighter.commandprocessor.Logging;
using SimpleInjector;

namespace ProtoBufReceiver.Configuration
{
    internal class Log4NetConfig
    {
        public static ILog Register(Container container)
        {
            XmlConfigurator.Configure();
            var logger = LogProvider.For<Program>();
            container.RegisterSingleton(logger);
            return logger;
        }
    }
}
