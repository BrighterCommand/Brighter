using System;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace Paramore.Brighter.Tests.MessagingGateway.redis
{
    [Collection("Redis")]
    [Trait("Category", "Redis")]
    public class RedisRequeueMessageTests : IClassFixture<RedisFixture>
    {
        private readonly RedisFixture _redisFixture;
        private readonly Message _messageOne;
        private readonly Message _messageTwo;


        public RedisRequeueMessageTests(RedisFixture redisFixture)
        {
            const string topic = "test";
            _redisFixture = redisFixture;
            _messageOne = new Message(
                new MessageHeader(Guid.NewGuid(), topic, MessageType.MT_COMMAND),
                new MessageBody("test content")
            );

            _messageTwo = new Message(
                new MessageHeader(Guid.NewGuid(), topic, MessageType.MT_COMMAND),
                new MessageBody("more test content")
            );

        }


        [Fact]
        public void When_requeing_a_failed_message()
        {
            //Need to receive to subscribe to feed, before we send a message. This returns an empty message we discard
            _redisFixture.MessageConsumer.Receive(1000);

            //Send a sequence of messages, we want to check that ordering is preserved
            _redisFixture.MessageProducer.Send(_messageOne);
            _redisFixture.MessageProducer.Send(_messageTwo);

            //Now receive, the first message 
            var sentMessageOne = _redisFixture.MessageConsumer.Receive(30000).Single();

            //now requeue the first message
            _redisFixture.MessageConsumer.Requeue(_messageOne, 300);

            //try receiving again; messageTwo should come first
            var sentMessageTwo = _redisFixture.MessageConsumer.Receive(30000).Single();
            var messageBodyTwo = sentMessageTwo.Body.Value;
            _redisFixture.MessageConsumer.Acknowledge(sentMessageTwo);
            
            sentMessageOne = _redisFixture.MessageConsumer.Receive(3000).Single();
            var messageBodyOne = sentMessageOne.Body.Value;
            _redisFixture.MessageConsumer.Acknowledge(sentMessageOne);


            //_should_send_a_message_via_restms_with_the_matching_body
            messageBodyOne.Should().Be(_messageOne.Body.Value);
            messageBodyTwo.Should().Be(_messageTwo.Body.Value);
        }
    }
}
