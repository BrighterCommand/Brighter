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
    public class MessagePumpToCommandProcessorDynamicMappingTests
    {
        private const string Channel = "MyChannel";
        private readonly RoutingKey _routingKey = new("MyTopic");
        private readonly InternalBus _bus = new ();
        private readonly FakeTimeProvider _timeProvider = new();
        private readonly IAmAMessagePump _messagePump;
        private readonly SpyCommandProcessor _commandProcessor;
        private readonly Channel _channel;

        public MessagePumpToCommandProcessorDynamicMappingTests()
        {
            _commandProcessor = new SpyCommandProcessor();
            _channel = new(
                new(Channel), _routingKey, 
                new InMemoryMessageConsumer(_routingKey, _bus, _timeProvider, TimeSpan.FromMilliseconds(1000))
            );
            
            var messagerMapperRegistry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory(type =>
                type switch
                    {
                        var t when t == typeof(MyEventMessageMapper) => new MyEventMessageMapper(),
                        var t when t == typeof(MyOtherEventMessageMapper) => new MyOtherEventMessageMapper(),
                        _ => throw new ArgumentException($"No mapper registered for type {type.FullName}", nameof(type))
                    }),
                null);
            messagerMapperRegistry.Register<MyEvent, MyEventMessageMapper>();
            messagerMapperRegistry.Register<MyOtherEvent, MyOtherEventMessageMapper>();
            
            _messagePump = new ServiceActivator.Reactor(_commandProcessor, (message) =>
                    message switch
                        {
                            var m when m.Header.Type == new CloudEventsType("io.brighter.paramore.myevent") => typeof(MyEvent), 
                            var m when m.Header.Type == new CloudEventsType("io.brighter.paramore.myotherevent") => typeof(MyOtherEvent),
                            _ => throw new ArgumentException($"No type mapping found for message with type {message.Header.Type}", nameof(message)),
                       }, 
                    messagerMapperRegistry, new EmptyMessageTransformerFactory(), new InMemoryRequestContextFactory(), _channel) 
                { Channel = _channel, TimeOut = TimeSpan.FromMilliseconds(5000) };
        }

        [Fact]
        public void When_Reading_A_MyOtherEvent_Message_From_A_Channel_Pump_Out_To_Command_Processor()
        {
            //arrange
            var @event = new MyEvent(); //although we send a MyEvent, we will map it dynamically to a MyOtherEvent   

            var message = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_EVENT, type: new CloudEventsType("io.brighter.paramore.myotherevent") ), 
                new MessageBody(JsonSerializer.Serialize(@event, JsonSerialisationOptions.Options))
            );
            
            _channel.Enqueue(message);
            var quitMessage = MessageFactory.CreateQuitMessage(_routingKey);
            _channel.Enqueue(quitMessage);
            
            //act
            _messagePump.Run();
            
            //assert

            Assert.Equal(CommandType.Publish, _commandProcessor.Commands[0]);
            var myOtherEvent = _commandProcessor.Observe<MyOtherEvent>();
            Assert.Equal(@event.Id, myOtherEvent.Id);
            Assert.Equal(@event.Data, myOtherEvent.Data);
        }
        
        [Fact]
        public void When_Reading_A_MyEvent_Message_From_A_Channel_Pump_Out_To_Command_Processor()
        {
            //arrange
            var @event = new MyEvent(); //we send a MyEvent, we will map it dynamically to a MyEvent   

            var message = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_EVENT, type: new CloudEventsType("io.brighter.paramore.myevent") ), 
                new MessageBody(JsonSerializer.Serialize(@event, JsonSerialisationOptions.Options))
            );
            
            _channel.Enqueue(message);
            var quitMessage = MessageFactory.CreateQuitMessage(_routingKey);
            _channel.Enqueue(quitMessage);
            
            //act
            _messagePump.Run();
            
            //assert

            Assert.Equal(CommandType.Publish, _commandProcessor.Commands[0]);
            var myEvent = _commandProcessor.Observe<MyEvent>();
            Assert.Equal(@event.Id, myEvent.Id);
            Assert.Equal(@event.Data, myEvent.Data);
        }
    }
}
