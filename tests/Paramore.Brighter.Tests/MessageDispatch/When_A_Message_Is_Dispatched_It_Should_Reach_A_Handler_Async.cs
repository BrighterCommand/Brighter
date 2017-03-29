using System;
using System.Threading.Tasks;
using FluentAssertions;
using Newtonsoft.Json;
using Xunit;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.ServiceActivator.TestHelpers;
using Paramore.Brighter.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.Tests.TestDoubles;

namespace Paramore.Brighter.Tests.MessageDispatch
{
    public class MessagePumpDispatchAsyncTests
    {
        private IAmAMessagePump _messagePump;
        private FakeChannel _channel;
        private IAmACommandProcessor _commandProcessor;
        private MyEvent _event;

        public MessagePumpDispatchAsyncTests()
        {
            var subscriberRegistry = new SubscriberRegistry();
            subscriberRegistry.RegisterAsync<MyEvent, MyEventHandlerAsyncWithContinuation>();

            _commandProcessor = new CommandProcessor(
                subscriberRegistry,
                new CheapHandlerFactoryAsync(), 
                new InMemoryRequestContextFactory(), 
                new PolicyRegistry());

            _channel = new FakeChannel();
            var mapper = new MyEventMessageMapper();
            _messagePump = new MessagePumpAsync<MyEvent>(_commandProcessor, mapper) { Channel = _channel, TimeoutInMilliseconds = 5000 };

            _event = new MyEvent();

            var message = new Message(new MessageHeader(Guid.NewGuid(), "MyTopic", MessageType.MT_EVENT), new MessageBody(JsonConvert.SerializeObject(_event)));
            _channel.Add(message);
            var quitMessage = new Message(new MessageHeader(Guid.Empty, "", MessageType.MT_QUIT), new MessageBody(""));
            _channel.Add(quitMessage);
        }

        [Fact]
        public async Task When_A_Message_Is_Dispatched_It_Should_Reach_A_Handler_Async()
        {
            await _messagePump.Run();

            MyEventHandlerAsyncWithContinuation.ShouldReceive(_event).Should().BeTrue();
            MyEventHandlerAsyncWithContinuation.LoopCounter.Value.Should().Be(2);
            MyEventHandlerAsyncWithContinuation.WorkThreadId.Should().Be(MyEventHandlerAsyncWithContinuation.ContinuationThreadId);
        }

        internal class CheapHandlerFactoryAsync : IAmAHandlerFactoryAsync
        {
            public IHandleRequestsAsync Create(Type handlerType)
            {
                if (handlerType == typeof(MyEventHandlerAsyncWithContinuation))
                {
                    return new MyEventHandlerAsyncWithContinuation();
                }
                return null;
            }

            public void Release(IHandleRequestsAsync handler)
            {
                var disposable = handler as IDisposable;
                disposable?.Dispose();
            }
        }
    }
}
