using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.Logging.TestDoubles;
using Xunit;
using Paramore.Brighter.Logging.Handlers;
using Polly.Registry;
using Serilog;
using Serilog.Sinks.TestCorrelator;
using Xunit.Abstractions;

namespace Paramore.Brighter.Core.Tests.Logging
{
    [Collection("Request Logger")]
    public class CommandProcessorWithLoggingInPipelineTests
    {
        private readonly ITestOutputHelper _output;

        public CommandProcessorWithLoggingInPipelineTests(ITestOutputHelper output)
        {
            _output = output;
        }

        //TODO: Because we use a global logger with Serilog, this won't run in parallel
        //[Fact]
        public void When_A_Request_Logger_Is_In_The_Pipeline()
        {
            Log.Logger = new LoggerConfiguration().MinimumLevel.Information().WriteTo.TestCorrelator().CreateLogger();
            using (var context = TestCorrelator.CreateContext())
            {
                var myCommand = new MyCommand();

                var registry = new SubscriberRegistry();
                registry.Register<MyCommand, IHandleRequests<MyCommand>>();
                var container = new ServiceCollection();


                var requestLogger = new RequestLoggingHandler<MyCommand>();

                container.AddTransient<IHandleRequests<MyCommand>, MyLoggedHandler>();
                container.AddTransient(typeof(RequestLoggingHandler<MyCommand>), provider => requestLogger);

                var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());

                var commandProcessor = new CommandProcessor(registry, handlerFactory, handlerFactory,
                    new InMemoryRequestContextFactory(), new PolicyRegistry());


                commandProcessor.Send(myCommand);

                //_should_log_the_request_handler_call
                //_should_log_the_type_of_handler_in_the_call

                //TestCorrelator.GetLogEventsFromCurrentContext().Should().HaveCount(3);
                TestCorrelator.GetLogEventsFromContextGuid(context.Guid)
                    .Should().Contain(x => x.MessageTemplate.Text.StartsWith("Logging handler pipeline call"))
                    .Which.Properties["1"].ToString().Should().Be($"\"{typeof(MyCommand)}\"");


                commandProcessor?.Dispose();
            }

        }

    }
}
