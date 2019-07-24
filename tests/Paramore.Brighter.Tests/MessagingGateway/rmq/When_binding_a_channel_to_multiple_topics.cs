using System;
using System.Linq;
using FluentAssertions;
using Paramore.Brighter.MessagingGateway.RMQ;
using Xunit;

namespace Paramore.Brighter.Tests.MessagingGateway.RMQ
{
    [Collection("RMQ")]
    [Trait("Category", "RMQ")]
    public class RmqMessageConsumerMultipleTopicTests : IDisposable
    {        
        private readonly IAmAMessageProducer _messageProducer;
        private readonly IAmAMessageConsumer _messageConsumer;
        private readonly Message _messageTopic1, _messageTopic2;

        public RmqMessageConsumerMultipleTopicTests()
        {
            _messageTopic1 = new Message(
                new MessageHeader(Guid.NewGuid(), Guid.NewGuid().ToString(), MessageType.MT_COMMAND), 
                new MessageBody("test content for topic test 1"));
            _messageTopic2 = new Message(
                new MessageHeader(Guid.NewGuid(), Guid.NewGuid().ToString(), MessageType.MT_COMMAND), 
                new MessageBody("test content for topic test 2"));

            var rmqConnection = new RmqMessagingGatewayConnection
            {
                AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
                Exchange = new Exchange("paramore.brighter.exchange")
            };

            var topics = new[] {_messageTopic1.Header.Topic, _messageTopic2.Header.Topic};
            var queueName = Guid.NewGuid().ToString();

            _messageProducer = new RmqMessageProducer(rmqConnection);
            _messageConsumer = new RmqMessageConsumer(rmqConnection, queueName , topics, false, false);

             new QueueFactory(rmqConnection, queueName, topics).Create(3000);
        }

        [Fact]
        public void When_reading_a_message_from_a_channel_with_multiple_topics()
        {
            _messageProducer.Send(_messageTopic1);
            _messageProducer.Send(_messageTopic2);

            var topic1Result = _messageConsumer.Receive(10000).First();
            _messageConsumer.Acknowledge(topic1Result);
            var topic2Result = _messageConsumer.Receive(10000).First();
            _messageConsumer.Acknowledge(topic2Result);

            // should_received_a_message_from_test1_with_same_topic_and_body
            topic1Result.Header.Topic.Should().Be(_messageTopic1.Header.Topic);
            topic1Result.Body.Value.Should().BeEquivalentTo(_messageTopic1.Body.Value);

            // should_received_a_message_from_test2_with_same_topic_and_body
            topic2Result.Header.Topic.Should().Be(_messageTopic2.Header.Topic);
            topic2Result.Body.Value.Should().BeEquivalentTo(_messageTopic2.Body.Value);            
        }

        public void Dispose()
        {
            _messageProducer.Dispose();
        }
    }
}
