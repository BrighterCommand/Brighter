using System;
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
    public class MessagePumpEmptyPauseTests 
    {
        private const int POLL_DELAY = 5000;
        private readonly IAmAMessagePump _messagePump;
        private readonly MyEvent _myEvent = new MyEvent();
        private FakeChannel _channel;

        public MessagePumpEmptyPauseTests()
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
            _messagePump = new MessagePumpBlocking<MyEvent>(commandProcessor, mapper) { Channel = _channel, TimeoutInMilliseconds = 5000, NoWorkPauseInMilliseconds = 5000};
 
            //how do we insert a quit, but be empty??
            //fake an empty message, which will trigger the same behaviour?
            var emptyMessage = new Message(new MessageHeader(Guid.Empty, "", MessageType.MT_NONE), new MessageBody(""));
            _channel.Enqueue(emptyMessage);
             
            //insert a quit to terminate the pump
            var quitMessage = new Message(new MessageHeader(Guid.Empty, "", MessageType.MT_QUIT), new MessageBody(""));
            _channel.Enqueue(quitMessage);
             
        }
        
        [Fact]
        public void When_a_channel_is_empty_then_delay()
        {
            _messagePump.Run();
            
            //channel should delay between messages
            _channel.PauseWaitInMs.Should().Be(POLL_DELAY);
        }
    }
}
