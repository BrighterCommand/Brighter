using System;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.ServiceActivator;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.Proactor
{
    public class MessagePumpToCommandProcessorTestsAsync
    {
        private const string Channel = "MyChannel";
        private readonly RoutingKey _routingKey = new("MyTopic");
        private readonly InternalBus _bus = new();
        private readonly FakeTimeProvider _timeProvider = new();
        private readonly IAmAMessagePump _messagePump;
        private readonly SpyCommandProcessor _commandProcessor;
        private readonly MyEvent _event;

        public MessagePumpToCommandProcessorTestsAsync()
        {
            _commandProcessor = new SpyCommandProcessor();
            ChannelAsync channel = new(
                new(Channel), _routingKey, 
                new InMemoryMessageConsumer(_routingKey, _bus, _timeProvider, TimeSpan.FromMilliseconds(1000))
            );
            var messagerMapperRegistry = new MessageMapperRegistry(
                null,
                new SimpleMessageMapperFactoryAsync(_ => new MyEventMessageMapperAsync()));
            messagerMapperRegistry.RegisterAsync<MyEvent, MyEventMessageMapperAsync>();
            
            _messagePump = new ServiceActivator.Proactor(_commandProcessor, (message) => typeof(MyEvent),
                    messagerMapperRegistry, new EmptyMessageTransformerFactoryAsync(), new InMemoryRequestContextFactory(), channel) 
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
            //although run does not return a Task, it will process handler and mapper asynchronously, using our
            //synchronization context. Messages should retain ordering of callbacks, so our test message should be processed
            //before we quit
            _messagePump.Run();

            Assert.Equal(CommandType.PublishAsync, _commandProcessor.Commands[0]);
            Assert.Equal(_event, _commandProcessor.Observe<MyEvent>());
        }
    }
}
