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

namespace Paramore.Brighter.Core.Tests.MessageDispatch.Proactor
{
    public class MessagePumpEventDontAckAggregateExceptionTestsAsync
    {
        private const string ChannelName = "MyChannel";
        private readonly RoutingKey _routingKey = new("MyTopic");
        private readonly InternalBus _bus = new();
        private readonly FakeTimeProvider _timeProvider = new();
        private readonly IAmAMessagePump _messagePump;
        private readonly SpyDontAckCommandProcessor _commandProcessor;

        public MessagePumpEventDontAckAggregateExceptionTestsAsync()
        {
            _commandProcessor = new SpyDontAckCommandProcessor();
            var channel = new ChannelAsync(
                new(ChannelName),
                _routingKey,
                new InMemoryMessageConsumer(_routingKey, _bus, _timeProvider, ackTimeout: TimeSpan.FromMilliseconds(1000))
            );

            var messageMapperRegistry = new MessageMapperRegistry(
                null,
                new SimpleMessageMapperFactoryAsync(_ => new MyEventMessageMapperAsync()));
            messageMapperRegistry.RegisterAsync<MyEvent, MyEventMessageMapperAsync>();

            _messagePump = new ServiceActivator.Proactor(
                _commandProcessor,
                (message) => typeof(MyEvent),
                messageMapperRegistry,
                new EmptyMessageTransformerFactoryAsync(),
                new InMemoryRequestContextFactory(),
                channel)
            {
                Channel = channel,
                TimeOut = TimeSpan.FromMilliseconds(5000),
                RequeueCount = -1,
                UnacceptableMessageLimit = 2,
                DontAckDelay = TimeSpan.Zero
            };

            // Arrange: enqueue two event messages (both will trigger AggregateException wrapping DontAckAction via PublishAsync)
            for (int i = 0; i < 2; i++)
            {
                var message = new Message(
                    new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_EVENT),
                    new MessageBody(JsonSerializer.Serialize(new MyEvent(), JsonSerialisationOptions.Options))
                );
                channel.Enqueue(message);
            }
        }

        [Fact]
        public void When_Aggregate_Exception_Containing_DontAck_Action_Should_Not_Acknowledge_Async()
        {
            // Act
            _messagePump.Run();

            // Assert: handler was called for both messages via PublishAsync (event path)
            Assert.Equal(CommandType.PublishAsync, _commandProcessor.Commands[0]);
            Assert.Equal(2, _commandProcessor.PublishCount);

            // Assert: pump continued running after the first AggregateException containing DontAckAction
            // (it processed the second event, proving it didn't crash or stop on the first)

            // Assert: unacceptable message count was incremented
            // The pump exited because the count reached the limit of 2,
            // which only happens if IncrementUnacceptableMessageCount was called for each DontAckAction
            Assert.Equal(MessagePumpStatus.MP_LIMIT_EXCEEDED, _messagePump.Status);

            // Assert: messages were nacked (re-enqueued to bus, available for redelivery)
            Assert.NotEmpty(_bus.Stream(_routingKey));
        }
    }
}
