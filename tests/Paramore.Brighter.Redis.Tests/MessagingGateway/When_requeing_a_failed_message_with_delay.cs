using System;
using System.Linq;
using FluentAssertions;
using Paramore.Brighter.Redis.Tests.Fixtures;
using Xunit;

namespace Paramore.Brighter.Redis.Tests.MessagingGateway
{
    [Collection("Redis")]
    [Trait("Category", "Redis")]
    public class RedisRequeueWithDelayTests : IClassFixture<RedisFixture>
    {
        private readonly RedisFixture _redisFixture;
        private readonly Message _messageOne;

        public RedisRequeueWithDelayTests(RedisFixture redisFixture)
        {
            const string topic = "test";
            _redisFixture = redisFixture;
            _messageOne = new Message(
                new MessageHeader(Guid.NewGuid(), topic, MessageType.MT_COMMAND),
                new MessageBody("test content")
            );
        }

        [Fact]
        public void When_requeing_a_failed_message_with_delay()
        {
            //clear the queue, and ensure it exists
            _redisFixture.Consumer.Receive(1000);
            
            //send & receive a message
            _redisFixture.Producer.Send(_messageOne);
            var message = _redisFixture.Consumer.Receive(1000).Single();
            message.Header.HandledCount.Should().Be(0);
            message.Header.DelayedMilliseconds.Should().Be(0);
            
            //now requeue with a delay
            _redisFixture.Consumer.Requeue(_messageOne, 1000);
            
            //receive and assert
            message = _redisFixture.Consumer.Receive(1000).Single();
            message.Header.HandledCount.Should().Be(1);
            message.Header.DelayedMilliseconds.Should().Be(1000);
        }
    }
}
