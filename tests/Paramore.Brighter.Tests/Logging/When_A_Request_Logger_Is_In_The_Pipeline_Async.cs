using System;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Paramore.Brighter.Logging.Handlers;
using Paramore.Brighter.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Tests.Logging.TestDoubles;
using Serilog;
using Serilog.Sinks.TestCorrelator;
using TinyIoC;

namespace Paramore.Brighter.Tests.Logging
{
    public class CommandProcessorWithLoggingInPipelineAsyncTests: IDisposable
    {
        private readonly MyCommand _myCommand;
        private readonly CommandProcessor _commandProcessor;

        public CommandProcessorWithLoggingInPipelineAsyncTests()
        {
            _myCommand = new MyCommand();

            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyCommand, MyLoggedHandlerAsync>();

            var container = new TinyIoCContainer();
            container.Register<IHandleRequestsAsync<MyCommand>, MyLoggedHandlerAsync>();
            container.Register<IHandleRequestsAsync<MyCommand>, RequestLoggingHandlerAsync<MyCommand>>();

            var handlerFactory = new TinyIocHandlerFactoryAsync(container);

            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry());
        }

        [Fact]
        public async Task When_A_Request_Logger_Is_In_The_Pipeline_Async()
        {
            if (!(Log.Logger is Serilog.Core.Logger))
                Log.Logger = new LoggerConfiguration().MinimumLevel.Information().WriteTo.TestCorrelator().CreateLogger();

            using (TestCorrelator.CreateContext())
            {
                await _commandProcessor.SendAsync(_myCommand);

                //_should_log_the_request_handler_call
                //_should_log_the_type_of_handler_in_the_call
                TestCorrelator.GetLogEventsFromCurrentContext()
                    .Should().Contain(x => x.MessageTemplate.Text.StartsWith("Logging handler pipeline call"))
                    .Which.Properties["1"].ToString().Should().Be($"\"{typeof(MyCommand)}\"");
            }
        }

        public void Dispose()
        {
            _commandProcessor?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
