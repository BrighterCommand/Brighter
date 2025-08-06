using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.Logging.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Logging.Handlers;
using Polly.Registry;
using Serilog;
using Serilog.Sinks.TestCorrelator;
using Xunit;
using Xunit.Abstractions;

namespace Paramore.Brighter.Core.Tests.Logging
{
    public class CommandProcessorWithLoggingInPipelineTests : IDisposable
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
            using var context = TestCorrelator.CreateContext();
            var myCommand = new MyCommand();

            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, IHandleRequests<MyCommand>>();

            var requestLogger = new RequestLoggingHandler<MyCommand>();

            var container = new ServiceCollection();
            container.AddTransient<MyLoggedHandler>();
            container.AddTransient(typeof(RequestLoggingHandler<MyCommand>), provider => requestLogger);

            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());

            var commandProcessor = new  CommandProcessor(registry, handlerFactory: handlerFactory, 
                new InMemoryRequestContextFactory(), new PolicyRegistry(), new ResiliencePipelineRegistry<string>(), 
                new InMemorySchedulerFactory());


            commandProcessor.Send(myCommand);

            var logEvents = TestCorrelator.GetLogEventsFromContextId(context.Id);
            Assert.Contains(logEvents, x => x.MessageTemplate.Text.StartsWith("Logging handler pipeline call"));
            Assert.Equal($"\"{typeof(MyCommand)}\"", logEvents.First(x => x.MessageTemplate.Text.StartsWith("Logging handler pipeline call")).Properties["1"].ToString());
        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}
