using System;
using Microsoft.Extensions.Time.Testing;

namespace Paramore.Brighter.Core.Tests.MessagingGateway
{
    public class BufferedChannelTests
    {
        private readonly IAmAChannelSync _channel;
        private readonly IAmAMessageConsumerSync _gateway;
        private const int BufferLimit = 2;
        private readonly RoutingKey _routingKey = new("MyTopic");
        private const string Channel = "MyChannel";
        private readonly InternalBus _bus = new();
        public BufferedChannelTests()
        {
            _gateway = new InMemoryMessageConsumer(new RoutingKey(_routingKey), _bus, new FakeTimeProvider(), ackTimeout: TimeSpan.FromMilliseconds(1000));
            _channel = new Channel(new(Channel), new(_routingKey), _gateway, BufferLimit);
        }

        [Test]
        public async Task When_the_buffer_is_not_empty_read_from_that_before_receiving()
        {
            //arrange
            var messageOne = new Message(new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_EVENT), new MessageBody("FirstMessage"));
            var messageTwo = new Message(new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_EVENT), new MessageBody("SecondMessage"));
            //put BufferLimit messages on the channel first
            _channel.Enqueue(messageOne, messageTwo);
            var messageThree = new Message(new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_EVENT), new MessageBody("ThirdMessage"));
            //put a message on the bus, to pull once the buffer is empty
            _bus.Enqueue(messageThree);
            //act
            var msgOne = _channel.Receive(TimeSpan.FromMilliseconds(10));
            var msgTwo = _channel.Receive(TimeSpan.FromMilliseconds(10));
            var msgThree = _channel.Receive(TimeSpan.FromMilliseconds(10));
            //assert
            await Assert.That(msgOne.Id).IsEqualTo(messageOne.Id);
            await Assert.That(msgTwo.Id).IsEqualTo(messageTwo.Id);
            await Assert.That(msgThree.Id).IsEqualTo(messageThree.Id);
        }

        [Test]
        public async Task When_the_buffer_is_replenished_allow_up_to_the_maximum_number_of_new_elements_to_enqueue()
        {
            //put BufferLimit messages on the queue first
            var messageOne = new Message(new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_EVENT), new MessageBody("FirstMessage"));
            var messageTwo = new Message(new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_EVENT), new MessageBody("SecondMessage"));
            var messageThree = new Message(new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_EVENT), new MessageBody("ThirdMessage"));
            // This should be fine
            _channel.Enqueue(messageOne, messageTwo, messageThree);
            //This should throw an exception
            await Assert.That(() => _channel.Enqueue(messageThree)).ThrowsExactly<InvalidOperationException>();
        }

        [Test]
        public async Task When_we_try_to_create_with_too_small_a_buffer()
        {
            await Assert.That(() => new Channel(new(Channel), new(_routingKey), _gateway, 0)).ThrowsExactly<ConfigurationException>();
        }

        [Test]
        public async Task When_we_try_to_create_with_too_large_a_buffer()
        {
            await Assert.That(() => new Channel(new(Channel), new(_routingKey), _gateway, 11)).ThrowsExactly<ConfigurationException>();
        }
    }
}