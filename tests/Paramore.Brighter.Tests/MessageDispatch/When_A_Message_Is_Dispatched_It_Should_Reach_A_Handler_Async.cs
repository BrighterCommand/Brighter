using System;
using System.Threading.Tasks;
using FluentAssertions;
using Newtonsoft.Json;
using Xunit;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.ServiceActivator.TestHelpers;
using Paramore.Brighter.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Tests.MessageDispatch.TestDoubles;

namespace Paramore.Brighter.Tests.MessageDispatch
{
    public class MessagePumpDispatchAsyncTests
    {
        private readonly IAmAMessagePump _messagePump;
        private readonly MyEvent _myEvent = new MyEvent();

        public MessagePumpDispatchAsyncTests()
        {
            var subscriberRegistry = new SubscriberRegistry();
            subscriberRegistry.RegisterAsync<MyEvent, MyEventHandlerAsyncWithContinuation>();

            var handlerFactory = new TestHandlerFactoryAsync<MyEvent, MyEventHandlerAsyncWithContinuation>(() => new MyEventHandlerAsyncWithContinuation());
            var commandProcessor = new CommandProcessor(
                subscriberRegistry,
                handlerFactory,
                new InMemoryRequestContextFactory(),
                new PolicyRegistry());

            var channel = new FakeChannel();
            var mapper = new MyEventMessageMapper();
            _messagePump = new MessagePumpAsync<MyEvent>(commandProcessor, mapper) { Channel = channel, TimeoutInMilliseconds = 5000 };

            var message = new Message(new MessageHeader(Guid.NewGuid(), "MyTopic", MessageType.MT_EVENT), new MessageBody(JsonConvert.SerializeObject(_myEvent)));
            channel.Add(message);
            var quitMessage = new Message(new MessageHeader(Guid.Empty, "", MessageType.MT_QUIT), new MessageBody(""));
            channel.Add(quitMessage);
        }

        [Fact]
        public async Task When_A_Message_Is_Dispatched_It_Should_Reach_A_Handler_Async()
        {
            await _messagePump.Run();

            MyEventHandlerAsyncWithContinuation.ShouldReceive(_myEvent).Should().BeTrue();
            MyEventHandlerAsyncWithContinuation.LoopCounter.Value.Should().Be(2);
            MyEventHandlerAsyncWithContinuation.WorkThreadId.Should().Be(MyEventHandlerAsyncWithContinuation.ContinuationThreadId);
        }
    }
}
