using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.Logging.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Logging.Handlers;
using Polly.Registry;
using Serilog;
using Serilog.Sinks.TestCorrelator;

namespace Paramore.Brighter.Core.Tests.Logging
{
    public class CommandProcessorWithLoggingInPipelineAsyncTests
    {
        //TODO: Because we use a global logger with Serilog, this won't run in parallel
        //[Test]
        public async Task When_A_Request_Logger_Is_In_The_Pipeline_Async()
        {
            Log.Logger = new LoggerConfiguration().MinimumLevel.Information().WriteTo.TestCorrelator().CreateLogger();
            using var context = TestCorrelator.CreateContext();
            var myCommand = new MyCommand();
            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyCommand, MyLoggedHandlerAsync>();
            var container = new ServiceCollection();
            container.AddTransient<MyLoggedHandlerAsync, MyLoggedHandlerAsync>();
            container.AddTransient(typeof(RequestLoggingHandlerAsync<>), typeof(RequestLoggingHandlerAsync<>));
            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());
            var commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(), new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory());
            await commandProcessor.SendAsync(myCommand);
            var logEvents = TestCorrelator.GetLogEventsFromContextId(context.Id);
            await Assert.That(logEvents).Contains(x => x.MessageTemplate.Text.StartsWith("Logging handler pipeline call"));
            await Assert.That(logEvents.First(x => x.MessageTemplate.Text.StartsWith("Logging handler pipeline call")).Properties["1"].ToString()).IsEqualTo($"\"{typeof(MyCommand)}\"");
        }
    }
}