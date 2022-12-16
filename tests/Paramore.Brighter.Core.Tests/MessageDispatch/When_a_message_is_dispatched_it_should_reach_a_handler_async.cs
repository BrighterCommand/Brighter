using System;
using System.Text.Json;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Xunit;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.ServiceActivator.TestHelpers;
using Polly.Registry;

namespace Paramore.Brighter.Core.Tests.MessageDispatch
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

            PipelineBuilder<MyEvent>.ClearPipelineCache();

            var channel = new FakeChannel();
            var messageMapperRegistry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory(_ => new MyEventMessageMapper()));
            messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();
            
             _messagePump = new MessagePumpAsync<MyEvent>(commandProcessor, messageMapperRegistry) 
                { Channel = channel, TimeoutInMilliseconds = 5000 };

            var message = new Message(new MessageHeader(Guid.NewGuid(), "MyTopic", MessageType.MT_EVENT), new MessageBody(JsonSerializer.Serialize(_myEvent)));
            channel.Enqueue(message);
            var quitMessage = new Message(new MessageHeader(Guid.Empty, "", MessageType.MT_QUIT), new MessageBody(""));
            channel.Enqueue(quitMessage);
        }

        [Fact(Timeout = 50000)]
        public void When_a_message_is_dispatched_it_should_reach_a_handler_async()
        {
            _messagePump.Run();

            MyEventHandlerAsyncWithContinuation.ShouldReceive(_myEvent).Should().BeTrue();
            MyEventHandlerAsyncWithContinuation.MonitorValue.Should().Be(2);
            //NOTE: We may want to run the continuation on the captured context, so as not to create a new thread, which means this test would 
            //change once we fix the pump to exhibit that behavior
            MyEventHandlerAsyncWithContinuation.WorkThreadId.Should().NotBe(MyEventHandlerAsyncWithContinuation.ContinuationThreadId);
        }
    }
  }
