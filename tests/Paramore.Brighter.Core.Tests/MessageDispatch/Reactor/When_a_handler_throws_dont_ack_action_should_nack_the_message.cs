using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.Testing;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.Reactor
{
    public class MessagePumpCommandDontAckActionNackTests
    {
        private const string ChannelName = "MyChannel";
        private readonly RoutingKey _routingKey = new("MyTopic");
        private readonly InternalBus _bus = new();
        private readonly FakeTimeProvider _timeProvider = new();
        private readonly IAmAMessagePump _messagePump;
        private readonly Channel _channel;
        private readonly SpyDontAckCommandProcessor _commandProcessor;
        public MessagePumpCommandDontAckActionNackTests()
        {
            _commandProcessor = new SpyDontAckCommandProcessor();
            _channel = new Channel(new(ChannelName), _routingKey, new InMemoryMessageConsumer(_routingKey, _bus, _timeProvider, ackTimeout: TimeSpan.FromMilliseconds(1000)));
            var messageMapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory(_ => new MyCommandMessageMapper()), null);
            messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();
            _messagePump = new ServiceActivator.Reactor(_commandProcessor, (message) => typeof(MyCommand), messageMapperRegistry, new EmptyMessageTransformerFactory(), new InMemoryRequestContextFactory(), _channel)
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
            var message = new Message(new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_COMMAND), new MessageBody(JsonSerializer.Serialize(new MyCommand(), JsonSerialisationOptions.Options)));
            _bus.Enqueue(message);
        }

        [Test]
        public async Task When_A_Handler_Throws_DontAck_Action_Should_Nack_The_Message()
        {
            // Act: run pump in background
            var task = Task.Factory.StartNew(() => _messagePump.Run(), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            // Wait for handler to process the message (DontAckAction thrown)
            var handled = await _commandProcessor.WaitForHandleAsync(5000);
            await Assert.That(handled).IsTrue();
            // Send quit to stop the pump after DontAckAction processing
            _channel.Enqueue(MessageFactory.CreateQuitMessage(_routingKey));
            await Task.WhenAll(task);
            // Assert: handler was called
            await Assert.That(_commandProcessor.SendCount >= 1).IsTrue();
            // Assert: pump continued running and processed the quit message
            await Assert.That(_messagePump.Status).IsEqualTo(MessagePumpStatus.MP_STOPPED);
            // Assert: message was nacked (re-enqueued to bus, available for redelivery)
            await Assert.That(_bus.Stream(_routingKey)).IsNotEmpty();
        }
    }
}
