using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.ServiceActivator;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.Reactor
{
    public class MessagePumpUnacceptableMessageLimitBreachedTests
    {
        private const string Channel = "MyChannel";
        private readonly IAmAMessagePump _messagePump;
        private readonly InternalBus _bus;
        private readonly FakeTimeProvider _timeProvider = new();
        private readonly RoutingKey _routingKey = new("MyTopic");

        public MessagePumpUnacceptableMessageLimitBreachedTests()
        {
            SpyRequeueCommandProcessor commandProcessor = new();

            _bus = new InternalBus();
            
            var channel = new Channel(new(Channel), _routingKey, new InMemoryMessageConsumer(_routingKey, _bus, _timeProvider, TimeSpan.FromMilliseconds(1000)), 3);
            var messageMapperRegistry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory(_ => new MyEventMessageMapper()),
                null);
            messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();
            
            _messagePump = new ServiceActivator.Reactor(commandProcessor, (message) => typeof(MyEvent), 
                messageMapperRegistry, null, new InMemoryRequestContextFactory(), channel)
            {
                Channel = channel, TimeOut = TimeSpan.FromMilliseconds(5000), RequeueCount = 3, UnacceptableMessageLimit = 3
            };

            var unacceptableMessage1 = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_UNACCEPTABLE), 
                new MessageBody("")
            );
            var unacceptableMessage2 = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_UNACCEPTABLE), 
                new MessageBody("")
            );
            var unacceptableMessage3 = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_UNACCEPTABLE), 
                new MessageBody("")
            );
            var unacceptableMessage4 = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_UNACCEPTABLE), 
                new MessageBody("")
            );

            channel.Enqueue(unacceptableMessage1);
            channel.Enqueue(unacceptableMessage2);
            channel.Enqueue(unacceptableMessage3);
            channel.Enqueue(unacceptableMessage4);
            
        }

        [Fact]
        public async Task When_An_Unacceptable_Message_Limit_Is_Reached()
        {
            var task = Task.Factory.StartNew(() => _messagePump.Run(), TaskCreationOptions.LongRunning);

            await Task.WhenAll(task);
            
            _timeProvider.Advance(TimeSpan.FromSeconds(2)); //This will trigger requeue of not acked/rejected messages

            Assert.Empty(_bus.Stream(_routingKey));

            //TODO: without inspection, how we would know you shut down? Observability?

        }
    }
}

