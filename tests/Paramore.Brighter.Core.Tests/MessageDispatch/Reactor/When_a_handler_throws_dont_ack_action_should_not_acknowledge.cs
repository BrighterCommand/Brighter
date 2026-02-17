using System;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.Testing;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.Reactor
{
    public class MessagePumpCommandDontAckActionTests
    {
        private const string ChannelName = "MyChannel";
        private readonly RoutingKey _routingKey = new("MyTopic");
        private readonly InternalBus _bus = new();
        private readonly FakeTimeProvider _timeProvider = new();
        private readonly IAmAMessagePump _messagePump;
        private readonly SpyDontAckCommandProcessor _commandProcessor;

        public MessagePumpCommandDontAckActionTests()
        {
            _commandProcessor = new SpyDontAckCommandProcessor();
            var channel = new Channel(
                new(ChannelName),
                _routingKey,
                new InMemoryMessageConsumer(_routingKey, _bus, _timeProvider, ackTimeout: TimeSpan.FromMilliseconds(1000))
            );

            var messageMapperRegistry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory(_ => new MyCommandMessageMapper()),
                null);
            messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();

            _messagePump = new ServiceActivator.Reactor(
                _commandProcessor,
                (message) => typeof(MyCommand),
                messageMapperRegistry,
                new EmptyMessageTransformerFactory(),
                new InMemoryRequestContextFactory(),
                channel)
            {
                Channel = channel,
                TimeOut = TimeSpan.FromMilliseconds(5000),
                RequeueCount = -1,
                UnacceptableMessageLimit = 2,
                DontAckDelay = TimeSpan.Zero
            };

            // Arrange: enqueue two command messages (both will trigger DontAckAction)
            // No quit message — the pump will exit via the unacceptable message limit
            for (int i = 0; i < 2; i++)
            {
                var message = new Message(
                    new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_COMMAND),
                    new MessageBody(JsonSerializer.Serialize(new MyCommand(), JsonSerialisationOptions.Options))
                );
                channel.Enqueue(message);
            }
        }

        [Fact]
        public void When_A_Handler_Throws_DontAck_Action_Should_Not_Acknowledge()
        {
            // Act
            _messagePump.Run();

            // Assert: handler was called for both messages
            Assert.Equal(CommandType.Send, _commandProcessor.Commands[0]);
            Assert.Equal(2, _commandProcessor.SendCount);

            // Assert: pump continued running after the first DontAckAction
            // (it processed the second command, proving it didn't crash or stop on the first)

            // Assert: unacceptable message count was incremented
            // The pump exited because the count reached the limit of 2,
            // which only happens if IncrementUnacceptableMessageCount was called for each DontAckAction
            Assert.Equal(MessagePumpStatus.MP_LIMIT_EXCEEDED, _messagePump.Status);

            // Assert: messages were nacked to the bus (available for redelivery)
            Assert.NotEmpty(_bus.Stream(_routingKey));
        }
    }
}
