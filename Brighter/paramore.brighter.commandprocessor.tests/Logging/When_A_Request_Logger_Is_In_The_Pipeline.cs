using System.Linq;
using Machine.Specifications;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.logging.Handlers;
using paramore.brighter.commandprocessor.Logging;
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;
using paramore.commandprocessor.tests.Logging.TestDoubles;
using TinyIoC;

namespace paramore.commandprocessor.tests.Logging
{
    [Subject(typeof(RequestLoggingHandler<>))]
    public class When_A_Request_Logger_Is_In_The_Pipeline
    {
        private static SpyLog s_logger;
        private static MyCommand s_myCommand;
        private static IAmACommandProcessor s_commandProcessor;

        private Establish _context = () =>
        {
            s_logger = new SpyLog();
            s_myCommand = new MyCommand();

            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyLoggedHandler>();
            var container = new TinyIoCContainer();
            container.Register<IHandleRequests<MyCommand>, MyLoggedHandler>();
            container.Register<IHandleRequests<MyCommand>, RequestLoggingHandler<MyCommand>>();
            container.Register<ILog>(s_logger);
           
            var handlerFactory = new TinyIocHandlerFactory(container);

            s_commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(), s_logger);
        };

        private Because _of = () => s_commandProcessor.Send(s_myCommand);

        private It _should_log_the_request_handler_call = () => s_logger.Logs.ShouldMatch(logs => logs.Any(log => log.Message.Contains("Logging handler pipeline call")));
        private It _should_log_the_type_of_handler_in_the_call = () => s_logger.Logs.ShouldMatch(logs => logs.Any( log => log.Message.Contains(typeof(MyCommand).ToString())));
    }
}
