using NUnit.Specifications;
using nUnitShouldAdapter;
using paramore.brighter.commandprocessor.logging.Handlers;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor.tests.nunit.CommandProcessors.TestDoubles;
using paramore.brighter.commandprocessor.tests.nunit.Logging.TestDoubles;
using System.Linq;
using TinyIoC;

namespace paramore.brighter.commandprocessor.tests.nunit.Logging
{
    [Subject(typeof(RequestLoggingHandler<>))]
    public class When_A_Request_Logger_Is_In_The_Pipeline : ContextSpecification
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
