using System;
using FluentAssertions;
using Xunit;
using Paramore.Brighter.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Tests.Logging.TestDoubles;
using Serilog;
using Serilog.Sinks.TestCorrelator;
using TinyIoC;
using Xunit.Abstractions;

namespace Paramore.Brighter.Tests.Logging
{
    
    public class CommandProcessorWithLoggingInPipelineTests : IClassFixture<LoggerFixture>, IDisposable
    {
        private readonly ITestOutputHelper _output;

        public CommandProcessorWithLoggingInPipelineTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void When_A_Request_Logger_Is_In_The_Pipeline()
        {
            using (TestCorrelator.CreateContext())
            {
                var myCommand = new MyCommand();

                var registry = new SubscriberRegistry();
                registry.Register<MyCommand, MyLoggedHandler>();

                var container = new TinyIoCContainer();
                container.Register<IHandleRequests<MyCommand>, MyLoggedHandler>();

                var handlerFactory = new TinyIocHandlerFactory(container);

                var commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry());

                commandProcessor.Send(myCommand);

                //_should_log_the_request_handler_call
                //_should_log_the_type_of_handler_in_the_call

                _output.WriteLine($"Logger Type: {Log.Logger}");
                foreach (var logEvent in TestCorrelator.GetLogEventsFromCurrentContext())
                {
                    _output.WriteLine(logEvent.MessageTemplate.Text);
                }
                
                //TestCorrelator.GetLogEventsFromCurrentContext().Should().HaveCount(3);
                TestCorrelator.GetLogEventsFromCurrentContext()
                    .Should().Contain(x => x.MessageTemplate.Text.StartsWith("Logging handler pipeline call"))
                    .Which.Properties["1"].ToString().Should().Be($"\"{typeof(MyCommand)}\"");
            }
        }


        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }

    public class LoggerFixture
    {
        public LoggerFixture()
        {
            Log.Logger = new LoggerConfiguration().MinimumLevel.Information().WriteTo.TestCorrelator().CreateLogger();
        }
    }
}
