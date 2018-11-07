using System;
using FluentAssertions;
using Xunit;

namespace Paramore.Brighter.Tests.MessagingGateway.redis
{
    [Collection("Redis")]
    [Trait("Category", "Redis")]
    public class RedisMessageProducerSendTests : IClassFixture<RedisFixture>
    {
        private readonly RedisFixture _redisFixture;
        private readonly Message _message;

        public RedisMessageProducerSendTests(RedisFixture redisFixture)
        {
            const string topic = "test";
            _redisFixture = redisFixture;
            _message = new Message(
                new MessageHeader(Guid.NewGuid(), topic, MessageType.MT_COMMAND), 
                new MessageBody("test content")
                );
        }
        
        
        [Fact]
        public void When_posting_a_message_via_the_messaging_gateway()
        {
            _redisFixture.MessageConsumer.Receive(1000); //Need to receive to subscribe to feed, before we send a message. This returns an empty message we discard
            _redisFixture.MessageProducer.Send(_message);
            var sentMessage = _redisFixture.MessageConsumer.Receive(1000);
            var messageBody = sentMessage.Body.Value;
            _redisFixture.MessageConsumer.Acknowledge(sentMessage);

            //_should_send_a_message_via_restms_with_the_matching_body
            messageBody.Should().Be(_message.Body.Value);
        }
    }
}
