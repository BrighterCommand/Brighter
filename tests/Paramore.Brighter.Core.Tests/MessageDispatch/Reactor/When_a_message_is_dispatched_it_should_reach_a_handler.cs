using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.ServiceActivator;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.Reactor
{
    public class MessagePumpDispatchTests
    {
        private readonly RoutingKey _routingKey = new("MyTopic");
        private readonly InternalBus _bus = new();
        private readonly FakeTimeProvider _timeProvider = new();
        private readonly IAmAMessagePump _messagePump;
        private readonly MyEvent _myEvent = new();
        private readonly IDictionary<string, string> _receivedMessages = new Dictionary<string, string>();

        public MessagePumpDispatchTests()
        {
            var subscriberRegistry = new SubscriberRegistry();
            subscriberRegistry.Register<MyEvent, MyEventHandler>();

            var handlerFactory = new SimpleHandlerFactorySync(_ => new MyEventHandler(_receivedMessages));

            var commandProcessor = new CommandProcessor(
                subscriberRegistry,
                handlerFactory, 
                new InMemoryRequestContextFactory(), 
                new PolicyRegistry(),
                new InMemorySchedulerFactory());
            
            PipelineBuilder<MyEvent>.ClearPipelineCache();

            var channel = new Channel(
                new("myChannel"), _routingKey, 
                new InMemoryMessageConsumer(_routingKey, _bus, _timeProvider, TimeSpan.FromMilliseconds(1000))
            );
            var messageMapperRegistry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory(
                    _ => new MyEventMessageMapper()),
                null); 
            messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();
            
            _messagePump = new ServiceActivator.Reactor(commandProcessor, (message) => typeof(MyEvent), 
                messageMapperRegistry, null, new InMemoryRequestContextFactory(), channel)
            {
                Channel = channel, TimeOut = TimeSpan.FromMilliseconds(5000)
            };

            var message = new Message(
                new MessageHeader(_myEvent.Id, _routingKey, MessageType.MT_EVENT), 
                new MessageBody(JsonSerializer.Serialize(_myEvent, JsonSerialisationOptions.Options))
            );
            
            channel.Enqueue(message);
            var quitMessage = MessageFactory.CreateQuitMessage(_routingKey);
            channel.Enqueue(quitMessage);
            
        }

        [Fact]
        public void When_A_Message_Is_Dispatched_It_Should_Reach_A_Handler()
        {
            _messagePump.Run();
            
            Assert.Contains(nameof(MyEventHandler), _receivedMessages);
            Assert.Equal(_myEvent.Id, _receivedMessages[nameof(MyEventHandler)]);
        }
    }
}
