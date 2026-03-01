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
    public class MessagePumpCommandDontAckActionTestsAsync
    {
        private const string ChannelName = "MyChannel";
        private readonly RoutingKey _routingKey = new("MyTopic");
        private readonly InternalBus _bus = new();
        private readonly FakeTimeProvider _timeProvider = new();
        private readonly IAmAMessagePump _messagePump;
        private readonly SpyDontAckCommandProcessor _commandProcessor;
        private readonly ChannelAsync _channel;

        public MessagePumpCommandDontAckActionTestsAsync()
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
                UnacceptableMessageLimit = 0,
                DontAckDelay = TimeSpan.Zero
            };

            // Arrange: enqueue the message to the BUS (not the channel's internal queue)
            // so that InMemoryMessageConsumer.Receive locks it in _lockedMessages.
            // This is essential for testing ack/no-ack behavior via the ack timeout mechanism.
            var message = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_COMMAND),
                new MessageBody(JsonSerializer.Serialize(new MyCommand(), JsonSerialisationOptions.Options))
            );
            _bus.Enqueue(message);
        }

        [Fact]
        public async Task When_A_Handler_Throws_DontAck_Action_Should_Not_Acknowledge_Async()
        {
            // Act: run the pump in the background
            var task = Task.Factory.StartNew(() => _messagePump.Run(), TaskCreationOptions.LongRunning);

            // Wait for the handler to be invoked (first delivery)
            Assert.True(_commandProcessor.WaitForHandle(), "Handler was not invoked within timeout");
            Assert.Equal(1, _commandProcessor.SendCount);

            // Advance time past the ack timeout to trigger requeue of unacknowledged messages.
            // The message was received via InMemoryMessageConsumer.Receive which locks it in _lockedMessages.
            // If the message was NOT acknowledged (DontAckAction handler with continue),
            // the ack timeout timer requeues it and the pump re-delivers it.
            // If the message WAS acknowledged (generic Exception handler without continue),
            // it was removed from _lockedMessages and no requeue occurs.
            _timeProvider.Advance(TimeSpan.FromSeconds(2));

            // Wait for the handler to be invoked again (re-delivery of unacknowledged message)
            Assert.True(_commandProcessor.WaitForHandle(), "Re-delivery did not occur - message may have been acknowledged");

            // Assert: handler was called at least twice (original delivery + re-delivery)
            // This proves the message was NOT acknowledged by the DontAckAction handler
            Assert.True(_commandProcessor.SendCount >= 2,
                $"Expected at least 2 sends (original + re-delivery), but got {_commandProcessor.SendCount}");

            // Clean up: send quit so pump can exit
            _channel.Enqueue(MessageFactory.CreateQuitMessage(_routingKey));

            // Let the pump finish
            await Task.WhenAll(task);
        }
    }
}
