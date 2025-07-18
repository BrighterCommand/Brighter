using System;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.ServiceActivator;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.Reactor
{
    public class MessagePumpToCommandProcessorTests
    {
        private const string Channel = "MyChannel";
        private readonly RoutingKey _routingKey = new("MyTopic");
        private readonly InternalBus _bus = new ();
        private readonly FakeTimeProvider _timeProvider = new();
        private readonly IAmAMessagePump _messagePump;
        private readonly SpyCommandProcessor _commandProcessor;
        private readonly MyEvent _event;

        public MessagePumpToCommandProcessorTests()
        {
            _commandProcessor = new SpyCommandProcessor();
            Channel channel = new(
                new(Channel), _routingKey, 
                new InMemoryMessageConsumer(_routingKey, _bus, _timeProvider, TimeSpan.FromMilliseconds(1000))
            );
            
            var messagerMapperRegistry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory(_ => new MyEventMessageMapper()),
                null);
            messagerMapperRegistry.Register<MyEvent, MyEventMessageMapper>();
            
            _messagePump = new ServiceActivator.Reactor(_commandProcessor, (message) => typeof(MyEvent), 
                    messagerMapperRegistry, new EmptyMessageTransformerFactory(), new InMemoryRequestContextFactory(), channel) 
                { Channel = channel, TimeOut = TimeSpan.FromMilliseconds(5000) };

            _event = new MyEvent();

            var message = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_EVENT), 
                new MessageBody(JsonSerializer.Serialize(_event, JsonSerialisationOptions.Options))
            );
            
            channel.Enqueue(message);
            var quitMessage = MessageFactory.CreateQuitMessage(_routingKey);
            channel.Enqueue(quitMessage);
            
        }

        [Fact]
        public void When_Reading_A_Message_From_A_Channel_Pump_Out_To_Command_Processor()
        {
            _messagePump.Run();

            Assert.Equal(CommandType.Publish, _commandProcessor.Commands[0]);
            Assert.Equal(_event, _commandProcessor.Observe<MyEvent>());
        }
    }
}
