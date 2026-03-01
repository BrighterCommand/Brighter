using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.Testing;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.Proactor
{
    public class MessagePumpCommandDontAckActionNackTestsAsync
    {
        private const string ChannelName = "MyChannel";
        private readonly RoutingKey _routingKey = new("MyTopic");
        private readonly InternalBus _bus = new();
        private readonly FakeTimeProvider _timeProvider = new();
        private readonly IAmAMessagePump _messagePump;
        private readonly ChannelAsync _channel;
        private readonly SpyDontAckCommandProcessor _commandProcessor;

        public MessagePumpCommandDontAckActionNackTestsAsync()
        {
            _commandProcessor = new SpyDontAckCommandProcessor();
            _channel = new ChannelAsync(
                new(ChannelName),
                _routingKey,
                new InMemoryMessageConsumer(_routingKey, _bus, _timeProvider, ackTimeout: TimeSpan.FromMilliseconds(1000))
            );

            var messageMapperRegistry = new MessageMapperRegistry(
                null,
                new SimpleMessageMapperFactoryAsync(_ => new MyCommandMessageMapperAsync()));
            messageMapperRegistry.RegisterAsync<MyCommand, MyCommandMessageMapperAsync>();

            _messagePump = new ServiceActivator.Proactor(
                _commandProcessor,
                (message) => typeof(MyCommand),
                messageMapperRegistry,
                new EmptyMessageTransformerFactoryAsync(),
                new InMemoryRequestContextFactory(),
                _channel)
            {
                Channel = _channel,
                TimeOut = TimeSpan.FromMilliseconds(5000),
                RequeueCount = -1,
                UnacceptableMessageLimit = -1,
                DontAckDelay = TimeSpan.FromMilliseconds(100)
            };

            // Arrange: enqueue one command message to the bus (not channel)
            // so InMemoryMessageConsumer.Receive locks it in _lockedMessages,
            // enabling nack to re-enqueue it back to the bus
            var message = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_COMMAND),
                new MessageBody(JsonSerializer.Serialize(new MyCommand(), JsonSerialisationOptions.Options))
            );
            _bus.Enqueue(message);
        }

        [Fact]
        public async Task When_A_Handler_Throws_DontAck_Action_Should_Nack_The_Message_Async()
        {
            // Act: run pump in background
            var task = Task.Factory.StartNew(() => _messagePump.Run(), TaskCreationOptions.LongRunning);

            // Wait for handler to process the message (DontAckAction thrown)
            var handled = _commandProcessor.WaitForHandle(5000);
            Assert.True(handled, "Handler should have been called");

            // Send quit to stop the pump after DontAckAction processing
            _channel.Enqueue(MessageFactory.CreateQuitMessage(_routingKey));

            await Task.WhenAll(task);

            // Assert: handler was called
            Assert.True(_commandProcessor.SendCount >= 1);

            // Assert: pump continued running and processed the quit message
            Assert.Equal(MessagePumpStatus.MP_STOPPED, _messagePump.Status);

            // Assert: message was nacked (re-enqueued to bus, available for redelivery)
            Assert.NotEmpty(_bus.Stream(_routingKey));
        }
    }
}
