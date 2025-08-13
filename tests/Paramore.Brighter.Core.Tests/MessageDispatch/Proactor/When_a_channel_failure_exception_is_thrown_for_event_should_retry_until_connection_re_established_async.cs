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
    public class MessagePumpRetryEventConnectionFailureTestsAsync
    {
        private readonly RoutingKey _routingKey = new("MyTopic");
        private readonly InternalBus _bus = new();
        private readonly FakeTimeProvider _timeProvider = new();
        private readonly IAmAMessagePump _messagePump;
        private readonly SpyCommandProcessor _commandProcessor;

        public MessagePumpRetryEventConnectionFailureTestsAsync()
        {
            _commandProcessor = new SpyCommandProcessor();
            var channel = new FailingChannelAsync(
                new ChannelName("myChannel"), _routingKey, 
                new InMemoryMessageConsumer(_routingKey, _bus, _timeProvider, TimeSpan.FromMilliseconds(1000)), 
                2)
            {
                NumberOfRetries = 1
            };
            
            var messageMapperRegistry = new MessageMapperRegistry(
                null,
                new SimpleMessageMapperFactoryAsync(_ => new MyEventMessageMapperAsync()));
            messageMapperRegistry.RegisterAsync<MyEvent, MyEventMessageMapperAsync>();
            
            _messagePump = new ServiceActivator.Proactor(_commandProcessor, (message) => typeof(MyEvent), 
                messageMapperRegistry, new EmptyMessageTransformerFactoryAsync(), new InMemoryRequestContextFactory(), channel)
            {
                Channel = channel, TimeOut = TimeSpan.FromMilliseconds(500), RequeueCount = -1
            };

            var @event = new MyEvent();

            //Two events will be received when channel fixed
            var message1 = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_EVENT), 
                new MessageBody(JsonSerializer.Serialize(@event, JsonSerialisationOptions.Options))
            );
            var message2 = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_EVENT), 
                new MessageBody(JsonSerializer.Serialize(@event, JsonSerialisationOptions.Options))
            );
            channel.Enqueue(message1);
            channel.Enqueue(message2);
            
            //Quit the message pump
            var quitMessage = MessageFactory.CreateQuitMessage(_routingKey);
            channel.Enqueue(quitMessage);
            
        }

        [Fact]
        public void When_A_Channel_Failure_Exception_Is_Thrown_For_Event_Should_Retry_Until_Connection_Re_established()
        {
            _messagePump.Run();

            //Should publish the message via the command processor
            Assert.Equal(2, _commandProcessor.Commands.Count);
            Assert.Equal(CommandType.PublishAsync, _commandProcessor.Commands[0]);
            Assert.Equal(CommandType.PublishAsync, _commandProcessor.Commands[1]);

        }

    }
}
