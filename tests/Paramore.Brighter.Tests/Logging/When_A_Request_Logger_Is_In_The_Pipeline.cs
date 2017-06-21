using System;
using FluentAssertions;
using Xunit;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Logging.Handlers;
using Paramore.Brighter.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Tests.Logging.TestDoubles;
using TinyIoC;

namespace Paramore.Brighter.Tests.Logging
{
    [Collection("Request Logging")]
    public class CommandProcessorWithLoggingInPipelineTests : IDisposable
    {
        private readonly SpyLog _logger;
        private readonly MyCommand _myCommand;
        private readonly IAmACommandProcessor _commandProcessor;

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

        [Fact]
        public void When_A_Request_Logger_Is_In_The_Pipeline()
        {
            _commandProcessor.Send(_myCommand);

            //_should_log_the_request_handler_call
            _logger.Logs.Should().Contain(log => log.Message.Contains("Logging handler pipeline call"));
            //_should_log_the_type_of_handler_in_the_call
            _logger.Logs.Should().Contain(log => log.Message.Contains(typeof(MyCommand).ToString()));
        }

        private void Release()
        {
            LogProvider.SetCurrentLogProvider(null);
        }

        public void Dispose()
        {
            Release();
            GC.SuppressFinalize(this);
        }

        ~CommandProcessorWithLoggingInPipelineTests()
        {
            Release();
        }
    }
}
