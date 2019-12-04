using System;
using System.Linq;
using FluentAssertions;
using Paramore.Brighter.MessagingGateway.RMQ;
using Xunit;

namespace Paramore.Brighter.RMQ.Tests.MessagingGateway
{
    [Collection("RMQ")]
    [Trait("Category", "RMQ")]
    public class RmqMessageProducerSendPersistentMessageTests : IDisposable
    {
        private IAmAMessageProducer _messageProducer;
        private IAmAMessageConsumer _messageConsumer;
        private Message _message;

        public RmqMessageProducerSendPersistentMessageTests()
        {
            _message = new Message(
                new MessageHeader(Guid.NewGuid(), Guid.NewGuid().ToString(), MessageType.MT_COMMAND),
                new MessageBody("test content"));

            var rmqConnection = new RmqMessagingGatewayConnection
            {
                AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
                Exchange = new Exchange("paramore.brighter.exchange"),
                PersistMessages = true
            };

            _messageProducer = new RmqMessageProducer(rmqConnection);
            _messageConsumer = new RmqMessageConsumer(rmqConnection, _message.Header.Topic, _message.Header.Topic, false);

            new QueueFactory(rmqConnection, _message.Header.Topic).Create(3000);
        }

        [Fact]
        public void When_posting_a_message_to_persist_via_the_messaging_gateway()
        {
            // arrange
            _messageProducer.Send(_message);

            // act
            var result = _messageConsumer.Receive(1000).First();

            // assert
            result.Persist.Should().Be(true);
        }

        public void Dispose()
        {
            _messageProducer.Dispose();
        }
    }
}

