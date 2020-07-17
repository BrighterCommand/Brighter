using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.Logging.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Logging.Handlers;
using Xunit;
using Polly.Registry;
using Serilog;
using Serilog.Sinks.TestCorrelator;
using Xunit.Abstractions;

namespace Paramore.Brighter.Core.Tests.Logging
{
    [Collection("Request Logger Async")]
    public class CommandProcessorWithLoggingInPipelineAsyncTests
    {

        private readonly ITestOutputHelper _output;

        public CommandProcessorWithLoggingInPipelineAsyncTests(ITestOutputHelper output)
        {
            _output = output;
        }

        //TODO: Because we use a global logger with Serilog, this won't run in parallel
        //[Fact]
        public async Task When_A_Request_Logger_Is_In_The_Pipeline_Async()
        {
            Log.Logger = new LoggerConfiguration().MinimumLevel.Information().WriteTo.TestCorrelator().CreateLogger();
            using (var context = TestCorrelator.CreateContext())
            {
                var myCommand = new MyCommand();

                var registry = new SubscriberRegistry();
                registry.RegisterAsync<MyCommand, MyLoggedHandlerAsync>();

                var container = new ServiceCollection();
                container.AddTransient<MyLoggedHandlerAsync, MyLoggedHandlerAsync>();
                container.AddTransient(typeof(RequestLoggingHandlerAsync<>), typeof(RequestLoggingHandlerAsync<>));

                var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());

                var commandProcessor = new CommandProcessor(registry, handlerFactory, handlerFactory,
                    new InMemoryRequestContextFactory(), new PolicyRegistry());


                await commandProcessor.SendAsync(myCommand);

                //_should_log_the_request_handler_call
                //_should_log_the_type_of_handler_in_the_call
                TestCorrelator.GetLogEventsFromContextGuid(context.Guid)
                    .Should().Contain(x => x.MessageTemplate.Text.StartsWith("Logging handler pipeline call"))
                    .Which.Properties["1"].ToString().Should().Be($"\"{typeof(MyCommand)}\"");

                commandProcessor?.Dispose();

            }
        }
    }
}
