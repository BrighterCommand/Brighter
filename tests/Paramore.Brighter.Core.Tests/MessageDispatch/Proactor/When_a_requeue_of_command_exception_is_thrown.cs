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
    public class MessagePumpCommandRequeueTestsAsync
    {
        private const string Channel = "MyChannel";
        private readonly RoutingKey _routingKey = new("MyTopic");
        private readonly InternalBus _bus = new();
        private readonly FakeTimeProvider _timeProvider = new();
        private readonly IAmAMessagePump _messagePump;
        private readonly SpyCommandProcessor _commandProcessor;
        private readonly MyCommand _command = new();

        public MessagePumpCommandRequeueTestsAsync()
        {
            _commandProcessor = new SpyRequeueCommandProcessor();
            ChannelAsync channel = new(new(Channel), _routingKey, new InMemoryMessageConsumer(_routingKey, _bus, _timeProvider, TimeSpan.FromMilliseconds(1000)), 2);
           
            var messageMapperRegistry = new MessageMapperRegistry(
                null,
                new SimpleMessageMapperFactoryAsync(_ => new MyCommandMessageMapperAsync()));
             messageMapperRegistry.RegisterAsync<MyCommand, MyCommandMessageMapperAsync>();
             
             _messagePump = new ServiceActivator.Proactor(_commandProcessor, (message) => typeof(MyCommand),
                     messageMapperRegistry, new EmptyMessageTransformerFactoryAsync(), new InMemoryRequestContextFactory(), channel) 
                { Channel = channel, TimeOut = TimeSpan.FromMilliseconds(5000), RequeueCount = -1 };

            var message1 = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_COMMAND), 
                new MessageBody(JsonSerializer.Serialize(_command, JsonSerialisationOptions.Options))
            );
            
            var message2 = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_COMMAND), 
                new MessageBody(JsonSerializer.Serialize(_command, JsonSerialisationOptions.Options))
            );
            
            channel.Enqueue(message1);
            channel.Enqueue(message2);
            var quitMessage = new Message(
                new MessageHeader(string.Empty, RoutingKey.Empty, MessageType.MT_QUIT), 
                new MessageBody("")
            );
            channel.Enqueue(quitMessage);
            
        }

        [Fact]
        public void When_A_Requeue_Of_Command_Exception_Is_Thrown()
        {
            _messagePump.Run();

            Assert.Equal(CommandType.SendAsync, _commandProcessor.Commands[0]);
            Assert.Equal(2, _bus.Stream(_routingKey).Count());
        }
    }
}
