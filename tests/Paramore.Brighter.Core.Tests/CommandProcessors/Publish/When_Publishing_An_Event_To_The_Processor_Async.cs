using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Publish
{
    [Collection("CommandProcessor")]
    public class CommandProcessorPublishEventAsyncTests : IDisposable
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly IDictionary<string, string> _receivedMessages = new Dictionary<string, string>();
        private readonly MyEvent _myEvent = new();

        public CommandProcessorPublishEventAsyncTests()
        {
            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyEvent, MyEventHandlerAsync>();
            var handlerFactory = new SimpleHandlerFactoryAsync(_ => new MyEventHandlerAsync(_receivedMessages));

            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), 
                new PolicyRegistry(), new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory());
            PipelineBuilder<MyEvent>.ClearPipelineCache();
        }

        [Fact]
        public async Task When_Publishing_An_Event_To_The_Processor()
        {
            await _commandProcessor.PublishAsync(_myEvent);

           //Should publish the command to the first event handler
           Assert.Contains(new KeyValuePair<string, string>(nameof(MyEventHandlerAsync), _myEvent.Id), _receivedMessages);

        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}
