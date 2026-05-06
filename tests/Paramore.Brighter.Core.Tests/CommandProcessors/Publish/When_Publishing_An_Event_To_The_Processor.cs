using System;
using System.Collections.Generic;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Polly.Registry;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Publish
{
    public class CommandProcessorPublishEventTests
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly IDictionary<string, string> _receivedMessages = new Dictionary<string, string>();
        private readonly MyEvent _myEvent = new();
        public CommandProcessorPublishEventTests()
        {
            var registry = new SubscriberRegistry();
            registry.Register<MyEvent, MyEventHandler>();
            var handlerFactory = new SimpleHandlerFactorySync(_ => new MyEventHandler(_receivedMessages));
            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(), new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory());
        }

        [Test]
        public async Task When_Publishing_An_Event_To_The_Processor()
        {
            _commandProcessor.Publish(_myEvent);
            //Should publish the command to the first event handler
            await Assert.That(_receivedMessages).Contains(new KeyValuePair<string, string>(nameof(MyEventHandler), _myEvent.Id));
        }
    }
}