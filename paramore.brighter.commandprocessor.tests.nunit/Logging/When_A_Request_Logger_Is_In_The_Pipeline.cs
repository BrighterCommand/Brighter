using paramore.brighter.commandprocessor.logging.Handlers;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor.tests.nunit.CommandProcessors.TestDoubles;
using paramore.brighter.commandprocessor.tests.nunit.Logging.TestDoubles;
using System.Linq;
using NUnit.Framework;
using TinyIoC;

namespace paramore.brighter.commandprocessor.tests.nunit.Logging
{
    [TestFixture]
    public class CommandProcessorWithLoggingInPipelineTests
    {
        private SpyLog _logger;
        private MyCommand _myCommand;
        private IAmACommandProcessor _commandProcessor;

        [SetUp]
        public void Establish()
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

        [Test]
        public void When_A_Request_Logger_Is_In_The_Pipeline()
        {
            _commandProcessor.Send(_myCommand);

            //_should_log_the_request_handler_call
            _logger.Logs.ShouldMatch(logs => logs.Any(log => log.Message.Contains("Logging handler pipeline call")));
            //_should_log_the_type_of_handler_in_the_call
            _logger.Logs.ShouldMatch(logs => logs.Any(log => log.Message.Contains(typeof(MyCommand).ToString())));
        }
    }
}
