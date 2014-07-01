using System.Collections.Specialized;
using Common.Logging;
using Common.Logging.Simple;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.ioccontainers.Adapters;
using TinyIoC;

namespace HelloWorld
{
    class Program
    {
        static void Main(string[] args)
        {
            var properties = new NameValueCollection();
            properties["showDateTime"] = "true";
            LogManager.Adapter = new ConsoleOutLoggerFactoryAdapter(properties);
            var logger = LogManager.GetLogger(typeof (Program));

            var builder = CommandProcessorBuilder.With()
                .InversionOfControl(new TinyIoCAdapter(new TinyIoCContainer()))
                .Logger(logger)
                .NoMessaging()
                .RequestContextFactory(new InMemoryRequestContextFactory());

            var commandProcessor = builder.Build();
        }
    }
}
