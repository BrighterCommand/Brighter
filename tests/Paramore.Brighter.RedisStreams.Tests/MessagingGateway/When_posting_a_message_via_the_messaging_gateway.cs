using System;
using System.Linq;
using FluentAssertions;
using Paramore.Brighter.RedisStreams.Tests.Fixtures;
using Xunit;

namespace Paramore.Brighter.RedisStreams.Tests.MessagingGateway
{
    public class RedisStreamMessageProducerSentTests : IClassFixture<RedisFixture>
    {
        private readonly RedisFixture _redisFixture;
        private Message _message;

        public RedisStreamMessageProducerSentTests(RedisFixture redisFixture)
        {
            _redisFixture = redisFixture;
            
            _message = new Message(
                new MessageHeader(Guid.NewGuid(), "Brighter_Test_Topic", MessageType.MT_EVENT), 
                new MessageBody("test content")
                );
 
        } 
        
        [Fact]
        public void When_posting_a_message_via_the_messaging_gateway()
        {
            _redisFixture.Consumer.Receive(1000); //Need to receive to subscribe to feed, before we send a message. This returns an empty message we discard
            _redisFixture.Producer.Send(_message);
            var sentMessage = _redisFixture.Consumer.Receive(1000).Single();
            var messageBody = sentMessage.Body.Value;
            _redisFixture.Consumer.Acknowledge(sentMessage);

            //_should_send_a_message_via_redies_streams_with_the_matching_body
            messageBody.Should().Be(_message.Body.Value);
        }
 
    }
}
