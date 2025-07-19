using System;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.ServiceActivator;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.Reactor
{
    public class MessagePumpRetryCommandOnConnectionFailureTests
    {
        private const string ChannelName = "myChannel";
        private readonly RoutingKey _routingKey = new("MyTopic");
        private readonly InternalBus _bus = new();
        private readonly FakeTimeProvider _timeProvider = new();
        private readonly IAmAMessagePump _messagePump;
        private readonly SpyCommandProcessor _commandProcessor;

        public MessagePumpRetryCommandOnConnectionFailureTests()
        {
            _commandProcessor = new SpyCommandProcessor();
            var channel = new FailingChannel(
                new ChannelName(ChannelName), _routingKey, 
                new InMemoryMessageConsumer(_routingKey, _bus, _timeProvider, TimeSpan.FromMilliseconds(1000)), 
                2)
            {
                NumberOfRetries = 1
            };
            var messageMapperRegistry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory(_ => new MyCommandMessageMapper()),
                null);
            messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();
            _messagePump = new ServiceActivator.Reactor(_commandProcessor, (message) => typeof(MyCommand),
                messageMapperRegistry, new EmptyMessageTransformerFactory(), new InMemoryRequestContextFactory(), channel)
            {
                Channel = channel, TimeOut = TimeSpan.FromMilliseconds(500), RequeueCount = -1
            };

            var command = new MyCommand();

            //two command, will be received when subscription restored
            var message1 = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_COMMAND), 
                new MessageBody(JsonSerializer.Serialize(command, JsonSerialisationOptions.Options))
            );
            var message2 = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_COMMAND), 
                new MessageBody(JsonSerializer.Serialize(command, JsonSerialisationOptions.Options))
            );
            channel.Enqueue(message1);
            channel.Enqueue(message2);
            
            //end the pump
            var quitMessage = MessageFactory.CreateQuitMessage(_routingKey);
            channel.Enqueue(quitMessage);
            
        }

        [Fact]
        public void When_A_Channel_Failure_Exception_Is_Thrown_For_Command_Should_Retry_Until_Connection_Re_established()
        {
            _messagePump.Run();

            //Should send the message via the command processor
            Assert.Equal(2, _commandProcessor.Commands.Count());
            Assert.Equal(CommandType.Send, _commandProcessor.Commands[0]);
            Assert.Equal(CommandType.Send, _commandProcessor.Commands[1]);
        }
    }
}
