using System;
using System.Linq;
using FluentAssertions;
using Paramore.Brighter.Redis.Tests.Fixtures;
using Xunit;

namespace Paramore.Brighter.Redis.Tests.MessagingGateway
{
    [Collection("Redis")]
    [Trait("Category", "Redis")]
    public class RedisMessageProducerMultipleSendTests : IClassFixture<RedisFixture>
    {
        private readonly RedisFixture _redisFixture;
        private readonly Message _messageOne;
        private readonly Message _messageTwo;

        public RedisMessageProducerMultipleSendTests(RedisFixture redisFixture)
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
        public void When_posting_a_message_via_the_messaging_gateway()
        {
            //Need to receive to subscribe to feed, before we send a message. This returns an empty message we discard
            _redisFixture.Consumer.Receive(1000); 
            
            //Send a sequence of messages, we want to check that ordering is preserved
            _redisFixture.Producer.Send(_messageOne);
            _redisFixture.Producer.Send(_messageTwo);
            
            //Now receive, and confirm order off is order on
            var sentMessageOne = _redisFixture.Consumer.Receive(1000).Single();
            var messageBodyOne = sentMessageOne.Body.Value;
            _redisFixture.Consumer.Acknowledge(sentMessageOne);
            
            var sentMessageTwo = _redisFixture.Consumer.Receive(1000).Single();
            var messageBodyTwo = sentMessageTwo.Body.Value;
            _redisFixture.Consumer.Acknowledge(sentMessageTwo);
            
            //_should_send_a_message_via_restms_with_the_matching_body
            messageBodyOne.Should().Be(_messageOne.Body.Value);
            messageBodyTwo.Should().Be(_messageTwo.Body.Value);
         }
    }
}
