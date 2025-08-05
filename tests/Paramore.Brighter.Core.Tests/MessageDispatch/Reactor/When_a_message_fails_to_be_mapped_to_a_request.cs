using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.ServiceActivator;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.Reactor
{

    public class MessagePumpFailingMessageTranslationTests
    {
        private const string Channel = "MyChannel";
        private readonly RoutingKey _routingKey = new RoutingKey("MyTopic");
        private readonly FakeTimeProvider _timeProvider = new();
        private readonly IAmAMessagePump _messagePump;
        private readonly Channel _channel;
        private readonly InternalBus _bus = new();

        public MessagePumpFailingMessageTranslationTests()
        {
            SpyRequeueCommandProcessor commandProcessor = new();
            _channel = new Channel(new(Channel), _routingKey, new InMemoryMessageConsumer(_routingKey, _bus, _timeProvider, TimeSpan.FromMilliseconds(1000)));
            var messageMapperRegistry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory(_ => new FailingEventMessageMapper()),
                null);
            messageMapperRegistry.Register<MyFailingMapperEvent, FailingEventMessageMapper>();
            var messageTransformerFactory = new SimpleMessageTransformerFactory(_ => throw new NotImplementedException());
             
            _messagePump = new ServiceActivator.Reactor(commandProcessor, (message) => typeof(MyFailingMapperEvent), 
                messageMapperRegistry, messageTransformerFactory, new InMemoryRequestContextFactory(), _channel)
            {
                Channel = _channel, TimeOut = TimeSpan.FromMilliseconds(5000), RequeueCount = 3, UnacceptableMessageLimit = 3
            };

            var unmappableMessage = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_EVENT), 
                new MessageBody("{ \"Id\" : \"48213ADB-A085-4AFF-A42C-CF8209350CF7\" }")
                );

            _bus.Enqueue(unmappableMessage);
            
        }

        [Fact]
        public async Task When_A_Message_Fails_To_Be_Mapped_To_A_Request ()
        {
            var task = Task.Factory.StartNew(() => _messagePump.Run(), TaskCreationOptions.LongRunning);

            await Task.Delay(2000);
            
            _timeProvider.Advance(TimeSpan.FromSeconds(2)); //This will trigger requeue of not acked/rejected messages

            _channel.Stop(_routingKey);

            await Task.WhenAll(task);

            Assert.Empty(_bus.Stream(_routingKey));
        }
    }
}
