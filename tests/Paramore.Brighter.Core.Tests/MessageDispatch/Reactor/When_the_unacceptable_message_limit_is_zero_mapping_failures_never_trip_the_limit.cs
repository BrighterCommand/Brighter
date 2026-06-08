using System;
using System.Linq;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.Testing;
using Paramore.Brighter.ServiceActivator;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.Reactor
{
    public class MessagePumpDefaultLimitZeroMappingFailuresNeverTripLimitTests
    {
        private const string ChannelName = "myChannel";
        private readonly RoutingKey _routingKey = new("MyTopic");
        private readonly RoutingKey _invalidMessageKey = new("MyInvalidMessageTopic");
        private readonly InternalBus _bus = new();
        private readonly FakeTimeProvider _timeProvider = new();
        private readonly IAmAMessagePump _messagePump;
        private readonly Channel _channel;

        public MessagePumpDefaultLimitZeroMappingFailuresNeverTripLimitTests()
        {
            _channel = new Channel(
                new(ChannelName), _routingKey,
                new InMemoryMessageConsumer(_routingKey, _bus, _timeProvider,
                    invalidMessageTopic: _invalidMessageKey,
                    ackTimeout: TimeSpan.FromMilliseconds(1000))
            );

            var messageMapperRegistry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory(_ => new FailingEventMessageMapper()),
                null);
            messageMapperRegistry.Register<MyFailingMapperEvent, FailingEventMessageMapper>();

            // UnacceptableMessageLimit deliberately NOT set (defaults to 0 = no limit)
            _messagePump = new ServiceActivator.Reactor(
                new SpyRequeueCommandProcessor(),
                (message) => typeof(MyFailingMapperEvent),
                messageMapperRegistry,
                null,
                new InMemoryRequestContextFactory(),
                _channel)
            {
                Channel = _channel,
                TimeOut = TimeSpan.FromMilliseconds(5000),
                RequeueCount = 3
            };

            var unmappableMessage = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_EVENT),
                new MessageBody("{ \"Id\" : \"48213ADB-A085-4AFF-A42C-CF8209350CF7\" }")
            );

            for (var i = 0; i < 100; i++)
                _bus.Enqueue(unmappableMessage);

            _bus.Enqueue(MessageFactory.CreateQuitMessage(_routingKey));
        }

        [Fact]
        public void When_The_Unacceptable_Message_Limit_Is_Zero_Mapping_Failures_Never_Trip_The_Limit()
        {
            // Act — pump runs until MT_QUIT; limit=0 never fires UnacceptableMessageLimitReached
            _messagePump.Run();

            // Assert — all 100 mapping failures were rejected (mechanism A)
            Assert.Equal(100, _bus.Stream(_invalidMessageKey).Count());

            // Assert — pump did not terminate due to the limit
            Assert.NotEqual(MessagePumpStatus.MP_LIMIT_EXCEEDED, _messagePump.Status);
        }
    }
}
