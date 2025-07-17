using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Extensions.DependencyInjection;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Publish
{
    [Collection("CommandProcessor")]
    public class CommandProcessorPublishMultipleMatchesAsyncTests : IDisposable
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly IDictionary<string, string> _receivedMessages = new Dictionary<string, string>();
        private readonly MyEvent _myEvent = new();
        private Exception? _exception;

        public CommandProcessorPublishMultipleMatchesAsyncTests()
        {
            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyEvent, MyEventHandlerAsync>();
            registry.RegisterAsync<MyEvent, MyOtherEventHandlerAsync>();

            var container = new ServiceCollection();
            container.AddTransient<MyEventHandlerAsync>();
            container.AddTransient<MyOtherEventHandlerAsync>();
            container.AddSingleton(_receivedMessages);
            container.AddSingleton<IBrighterOptions>(new BrighterOptions {HandlerLifetime = ServiceLifetime.Transient});

            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());


            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(), new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory());
            PipelineBuilder<MyEvent>.ClearPipelineCache();
        }

        //Ignore any errors about adding System.Runtime from the IDE. See https://social.msdn.microsoft.com/Forums/en-US/af4dc0db-046c-4728-bfe0-60ceb93f7b9f/vs2012net-45-rc-compiler-error-when-using-actionblock-missing-reference-to?forum=tpldataflow
        [Fact]
        public async Task When_There_Are_Multiple_Subscribers_Async()
        {
            _exception = await Catch.ExceptionAsync(async () => await _commandProcessor.PublishAsync(_myEvent));

            //Should not throw an exception
            Assert.Null(_exception);
            //Should publish the command to the first event handler
            Assert.Contains(new KeyValuePair<string, string>(nameof(MyEventHandlerAsync), _myEvent.Id), _receivedMessages);
            //Should publish the command to the second event handler
            Assert.Contains(new KeyValuePair<string, string>(nameof(MyOtherEventHandlerAsync), _myEvent.Id), _receivedMessages);
        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}
