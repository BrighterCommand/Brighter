using System;
using System.Collections.Generic;
using System.Text.Json;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.ServiceActivator.TestHelpers;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch
{
    public class MessagePumpDelayTests 
    {
        private const int POLL_DELAY = 5000;
        private readonly IAmAMessagePump _messagePump;
        private readonly MyEvent _myEvent = new MyEvent();
        private FakeChannel _channel;

        public MessagePumpDelayTests()
        {
            var subscriberRegistry = new SubscriberRegistry();
            subscriberRegistry.Register<MyEvent, MyEventHandler>();

            var handlerFactory = new TestHandlerFactory<MyEvent, MyEventHandler>(() => new MyEventHandler());

            var commandProcessor = new CommandProcessor(
                subscriberRegistry,
                handlerFactory, 
                new InMemoryRequestContextFactory(), 
                new PolicyRegistry());
            
            PipelineBuilder<MyEvent>.ClearPipelineCache();

            _channel = new FakeChannel();
            var mapper = new MyEventMessageMapper();
            _messagePump = new MessagePumpBlocking<MyEvent>(commandProcessor, mapper) { Channel = _channel, TimeoutInMilliseconds = 5000, PollDelayInMilliseconds = POLL_DELAY};

            var message = new Message(new MessageHeader(Guid.NewGuid(), "MyTopic", MessageType.MT_EVENT), new MessageBody(JsonSerializer.Serialize(_myEvent, JsonSerialisationOptions.Options)));
            _channel.Enqueue(message);
            var quitMessage = new Message(new MessageHeader(Guid.Empty, "", MessageType.MT_QUIT), new MessageBody(""));
            _channel.Enqueue(quitMessage);
 
        }
        
        [Fact]
        public void When_a_message_dispatcher_has_a_delay()
        {
            _messagePump.Run();
            
            //channel should delay between messages
            _channel.PauseWaitInMs.Should().Be(POLL_DELAY);
        }
    }
}
