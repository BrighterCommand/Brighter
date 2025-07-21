using System;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.ServiceActivator;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.Proactor
{
    public class MessagePumpEventRequeueTestsAsync
    {
        private const string Channel = "MyChannel";
        private readonly RoutingKey _routingKey = new("MyTopic");
        private readonly InternalBus _bus = new();
        private readonly FakeTimeProvider _timeProvider = new();
        private readonly IAmAMessagePump _messagePump;
        private readonly SpyCommandProcessor _commandProcessor;

        public MessagePumpEventRequeueTestsAsync()
        {
            _commandProcessor = new SpyRequeueCommandProcessor();
            ChannelAsync channel = new(
                new(Channel), _routingKey, 
                new InMemoryMessageConsumer(_routingKey, _bus, _timeProvider, TimeSpan.FromMilliseconds(1000)),
                2
            );
            
            var messageMapperRegistry = new MessageMapperRegistry(
                null,
                new SimpleMessageMapperFactoryAsync(_ => new MyEventMessageMapperAsync()));
            messageMapperRegistry.RegisterAsync<MyEvent, MyEventMessageMapperAsync>();
             
            _messagePump = new ServiceActivator.Proactor(_commandProcessor, (message) => typeof(MyEvent), 
                    messageMapperRegistry, new EmptyMessageTransformerFactoryAsync(), new InMemoryRequestContextFactory(), channel) 
                { Channel = channel, TimeOut = TimeSpan.FromMilliseconds(5000), RequeueCount = -1 };

            var message1 = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_EVENT), 
                new MessageBody(JsonSerializer.Serialize((MyEvent)new(), JsonSerialisationOptions.Options))
            );
            var message2 = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_EVENT), 
                new MessageBody(JsonSerializer.Serialize((MyEvent)new(), JsonSerialisationOptions.Options))
            );
            
            channel.Enqueue(message1);
            channel.Enqueue(message2);
            var quitMessage = MessageFactory.CreateQuitMessage(new RoutingKey("MyTopic"));
            channel.Enqueue(quitMessage);
            
        }

        [Fact]
        public void When_A_Requeue_Of_Event_Exception_Is_Thrown()
        {
            _messagePump.Run();
            
            _timeProvider.Advance(TimeSpan.FromSeconds(2)); //This will trigger requeue of not acked/rejected messages

            //_should_publish_the_message_via_the_command_processor
            Assert.Equal(CommandType.PublishAsync, _commandProcessor.Commands[0]);

            //_should_requeue_the_messages
            Assert.Equal(2, _bus.Stream(_routingKey).Count());

            
            //TODO: How do we know that the channel has been disposed? Observability
        }
    }
}
