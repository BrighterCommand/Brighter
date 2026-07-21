using System;
using System.Linq;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.Testing;
using Paramore.Brighter.ServiceActivator;
using System.Threading.Tasks;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.Proactor
{
    public class MessagePumpDefaultLimitZeroMappingFailuresNeverTripLimitAsyncTests
    {
        private const string ChannelName = "myChannel";
        private readonly RoutingKey _routingKey = new("MyTopic");
        private readonly RoutingKey _invalidMessageKey = new("MyInvalidMessageTopic");
        private readonly InternalBus _bus = new();
        private readonly FakeTimeProvider _timeProvider = new();
        private readonly IAmAMessagePump _messagePump;
        private readonly ChannelAsync _channel;

        public MessagePumpDefaultLimitZeroMappingFailuresNeverTripLimitAsyncTests()
        {
            _channel = new ChannelAsync(
                new(ChannelName), _routingKey,
                new InMemoryMessageConsumer(_routingKey, _bus, _timeProvider,
                    invalidMessageTopic: _invalidMessageKey,
                    ackTimeout: TimeSpan.FromMilliseconds(1000))
            );

            var messageMapperRegistry = new MessageMapperRegistry(
                null,
                new SimpleMessageMapperFactoryAsync(_ => new FailingEventMessageMapperAsync()));
            messageMapperRegistry.RegisterAsync<MyFailingMapperEvent, FailingEventMessageMapperAsync>();

            // UnacceptableMessageLimit deliberately NOT set (defaults to 0 = no limit)
            _messagePump = new ServiceActivator.Proactor(
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

        [Test]
        public async Task When_The_Unacceptable_Message_Limit_Is_Zero_Mapping_Failures_Never_Trip_The_Limit()
        {
            // Act — pump runs until MT_QUIT; limit=0 never fires UnacceptableMessageLimitReached
            _messagePump.Run();

            // Assert — all 100 mapping failures were rejected (mechanism A)
            await Assert.That(_bus.Stream(_invalidMessageKey).Count()).IsEqualTo(100);

            // Assert — pump did not terminate due to the limit
            await Assert.That(_messagePump.Status).IsNotEqualTo(MessagePumpStatus.MP_LIMIT_EXCEEDED);
        }
    }
}