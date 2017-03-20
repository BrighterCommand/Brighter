using System.Linq;
using Xunit;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Logging.Handlers;
using Paramore.Brighter.Tests.Logging.TestDoubles;
using Paramore.Brighter.Tests.TestDoubles;
using TinyIoC;

namespace Paramore.Brighter.Tests.Logging
{
    public class CommandProcessorWithLoggingInPipelineTests
    {
        private SpyLog _logger;
        private MyCommand _myCommand;
        private IAmACommandProcessor _commandProcessor;

        public CommandProcessorWithLoggingInPipelineTests()
        {
            _logger = new SpyLog();
            _myCommand = new MyCommand();

            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyLoggedHandler>();

            var container = new TinyIoCContainer();
            container.Register<IHandleRequests<MyCommand>, MyLoggedHandler>();
            container.Register<IHandleRequests<MyCommand>, RequestLoggingHandler<MyCommand>>();

            var handlerFactory = new TinyIocHandlerFactory(container);

            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry());

            LogProvider.SetCurrentLogProvider(new SpyLogProvider(_logger));
        }

        [Fact(Skip = "TODO: Fails erratically to find messages in pipeline")]
        public void When_A_Request_Logger_Is_In_The_Pipeline()
        {
            _commandProcessor.Send(_myCommand);

            //_should_log_the_request_handler_call
            Assert.True(_logger.Logs.Any(log => log.Message.Contains("Logging handler pipeline call")), "Could not find the call to the logging pipeline");
            //_should_log_the_type_of_handler_in_the_call
            Assert.True(_logger.Logs.Any(log => log.Message.Contains(typeof(MyCommand).ToString())), "Could not find the command in the logs");
        }
    }
}
