using log4net.Config;
using SimpleInjector;

namespace ProtoBufSender.Configuration
{
    static class Log4NetConfig
    {
        public static void Register(Container container)
        {
            XmlConfigurator.Configure();
        }
    }
}
