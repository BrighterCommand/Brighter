using System;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.ServiceActivator;
using Polly.Registry;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.Proactor
{
    public class MessagePumpDispatchAsyncTests
    {
        private const string ChannelName = "myChannel";
        private readonly RoutingKey _routingKey = new("MyTopic");
        private readonly InternalBus _bus = new();
        private readonly FakeTimeProvider _timeProvider = new();
        private readonly IAmAMessagePump _messagePump;
        private readonly MyEvent _myEvent = new MyEvent();
        public MessagePumpDispatchAsyncTests()
        {
            var subscriberRegistry = new SubscriberRegistry();
            subscriberRegistry.RegisterAsync<MyEvent, MyEventHandlerAsyncWithContinuation>();
            var handlerFactory = new SimpleHandlerFactoryAsync(_ => new MyEventHandlerAsyncWithContinuation());
            var commandProcessor = new CommandProcessor(subscriberRegistry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(), new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory());
            var channel = new ChannelAsync(new(ChannelName), _routingKey, new InMemoryMessageConsumer(_routingKey, _bus, _timeProvider, ackTimeout: TimeSpan.FromMilliseconds(1000)));
            var messageMapperRegistry = new MessageMapperRegistry(null, new SimpleMessageMapperFactoryAsync(_ => new MyEventMessageMapperAsync()));
            messageMapperRegistry.RegisterAsync<MyEvent, MyEventMessageMapperAsync>();
            _messagePump = new ServiceActivator.Proactor(commandProcessor, (message) => typeof(MyEvent), messageMapperRegistry, new EmptyMessageTransformerFactoryAsync(), new InMemoryRequestContextFactory(), channel)
            {
                Channel = channel,
                TimeOut = TimeSpan.FromMilliseconds(5000)
            };
            var message = new Message(new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_EVENT), new MessageBody(JsonSerializer.Serialize(_myEvent)));
            channel.Enqueue(message);
            var quitMessage = MessageFactory.CreateQuitMessage(_routingKey);
            channel.Enqueue(quitMessage);
        }

        [Test]
        public async Task When_a_message_is_dispatched_it_should_reach_a_handler_async()
        {
            _messagePump.Run();
            await Assert.That(MyEventHandlerAsyncWithContinuation.ShouldReceive(_myEvent)).IsTrue();
            await Assert.That(MyEventHandlerAsyncWithContinuation.MonitorValue).IsEqualTo(2);
            //NOTE: We may want to run the continuation on the captured context, so as not to create a new thread, which means this test would 
            //change once we fix the pump to exhibit that behavior\
            await Assert.That(MyEventHandlerAsyncWithContinuation.ContinuationThreadId).IsNotEqualTo(MyEventHandlerAsyncWithContinuation.WorkThreadId);
        }
    }
}