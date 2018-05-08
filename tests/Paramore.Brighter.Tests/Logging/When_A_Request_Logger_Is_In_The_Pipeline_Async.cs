using System;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Logging.Handlers;
using Paramore.Brighter.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Tests.Logging.TestDoubles;
using TinyIoC;

namespace Paramore.Brighter.Tests.Logging
{
    [Collection("Request Logging Async")]
    public class CommandProcessorWithLoggingInPipelineAsyncTests: IDisposable
    {
        private readonly SpyLog _logger;
        private readonly MyCommand _myCommand;
        private readonly CommandProcessor _commandProcessor;

        public CommandProcessorWithLoggingInPipelineAsyncTests()
        {
            _logger = new SpyLog();
            _myCommand = new MyCommand();

            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyCommand, MyLoggedHandlerAsync>();

            var container = new TinyIoCContainer();
            container.Register<IHandleRequestsAsync<MyCommand>, MyLoggedHandlerAsync>();
            container.Register<IHandleRequestsAsync<MyCommand>, RequestLoggingHandlerAsync<MyCommand>>();

            var handlerFactory = new TinyIocHandlerFactoryAsync(container);

            LogProvider.SetCurrentLogProvider(new SpyLogProvider(_logger));
            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry());
        }

        [Fact]
        public async Task When_A_Request_Logger_Is_In_The_Pipeline_Async()
        {
            await _commandProcessor.SendAsync(_myCommand);

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
            _commandProcessor?.Dispose();
            Release();
            GC.SuppressFinalize(this);
        }

        ~CommandProcessorWithLoggingInPipelineAsyncTests()
        {
            Release();
        }
    }
}
