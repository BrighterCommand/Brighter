using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Publish
{
    [Collection("CommandProcessor")]
    public class CommandProcessorPublishEventAgreementAsyncTests : IDisposable
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly IDictionary<string, string> _receivedMessages = new Dictionary<string, string>();

        public CommandProcessorPublishEventAgreementAsyncTests()
        {
            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyEvent>((request, context) =>
            {
                var myEvent = request as MyEvent;
                
                if (myEvent.Data == 4)
                    return [typeof(MyEventHandlerAsync)];
                
                return [..Array.Empty<Type>()];
            },
                [typeof(MyEventHandlerAsync)]);
            var handlerFactory = new SimpleHandlerFactoryAsync(_ => new MyEventHandlerAsync(_receivedMessages));

            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), 
                new PolicyRegistry(), new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory());
            PipelineBuilder<MyEvent>.ClearPipelineCache();
        }

        [Fact]
        public async Task When_Publishing_An_Event_To_The_Processor()
        {
            var myEvent = new MyEvent { Data = 4 };
            await _commandProcessor.PublishAsync(myEvent);

           //Should publish the command to the first event handler
           Assert.Contains(new KeyValuePair<string, string>(nameof(MyEventHandlerAsync), myEvent.Id), _receivedMessages);

        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}
