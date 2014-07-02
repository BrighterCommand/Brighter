using System;
using System.Collections.Specialized;
using Common.Logging;
using Common.Logging.Simple;
using paramore.brighter.commandprocessor;

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

            var registry = new TargetHandlerRegistry(); 
            registry.Register<GreetingCommand, GreetingCommandHandler>();


            var builder = CommandProcessorBuilder.With()
                .Handlers(new HandlerConfiguration(
                     targetHandlerRegistry: new TargetHandlerRegistry(),
                     handlerFactory: new TinyIoCHandlerFactory(logger)
                    ))
                .NoPolicy()
                .Logger(logger)
                .NoMessaging()
                .RequestContextFactory(new InMemoryRequestContextFactory());

            var commandProcessor = builder.Build();

            commandProcessor.Send(new GreetingCommand("Ian"));
        }

        internal class TinyIoCHandlerFactory : IAmAHandlerFactory
        {
            private readonly ILog logger;

            public TinyIoCHandlerFactory(ILog logger)
            {
                this.logger = logger;
            }

            public IHandleRequests Create(Type handlerType)
            {
                return new GreetingCommandHandler(logger);
            }

            public void Release(IHandleRequests handler)
            {
            }
        }
    }

 
}
