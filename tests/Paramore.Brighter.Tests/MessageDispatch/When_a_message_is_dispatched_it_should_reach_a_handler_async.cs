using System;
using System.Threading;
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
            _messagePump = new MessagePumpAsync<MyEvent>(channel, commandProcessor, mapper)
            {
                TimeoutInMilliseconds = 5000
            };

            var message = new Message(new MessageHeader(Guid.NewGuid(), "MyTopic", MessageType.MT_EVENT), new MessageBody(JsonConvert.SerializeObject(_myEvent)));
            channel.Add(message);
            var quitMessage = new Message(new MessageHeader(Guid.Empty, "", MessageType.MT_QUIT), new MessageBody(""));
            channel.Add(quitMessage);
        }

        [Fact(Skip = "Failing due to threading issues on Xunit tests")]
        public async Task When_a_message_is_dispatched_it_should_reach_a_handler_async()
        {
            var cts = new CancellationTokenSource();
            await _messagePump.RunAsync(cts.Token);

            MyEventHandlerAsyncWithContinuation.ShouldReceive(_myEvent).Should().BeTrue();
            MyEventHandlerAsyncWithContinuation.MonitorValue.Should().Be(2);
            //NOTE: We may want to run the continuation on the captured context, so as not to create a new thread, which means this test would 
            //change once we fix the pump to exhibit that behavior
            MyEventHandlerAsyncWithContinuation.WorkThreadId.Should().NotBe(MyEventHandlerAsyncWithContinuation.ContinuationThreadId);
        }
    }
}
