using System;
using FluentAssertions;
using Paramore.Brighter.MessagingGateway.Redis;
using Xunit;

namespace Paramore.Brighter.Tests.MessagingGateway.redis
{
    [Trait("Category", "Redis")]
    public class RedisGoodMessageParsingTests
    {
        private const string GoodMessage =
            "<HEADER\n{\"TimeStamp\":\"2018-02-07T09:38:36Z\",\"Id\":\"18669550-2069-48c5-923d-74a2e79c0748\",\"Topic\":\"test\",\"MessageType\":1,\"Bag\":\"{}\",\"HandledCount\":3,\"DelayedMilliseconds\":200,\"CorrelationId\":\"0AF88BBC-07FD-4FC3-9CA7-BF68415A2535\",\"ContentType\":\"text/plain\",\"ReplyTo\":\"reply.queue\"}\nHEADER/>\n<BODY\nmore test content\nBODY/>";
        
        [Fact]
        public void When_parsing_a_good_redis_message_to_brighter()
        {
            var redisMessageCreator = new RedisMessageCreator();
            
            Message message = redisMessageCreator.CreateMessage(GoodMessage);

            message.Id.Should().Be(Guid.Parse("18669550-2069-48c5-923d-74a2e79c0748"));
            message.Header.TimeStamp.Should().Be(DateTime.Parse("2018-02-07T09:38:36Z"));
            message.Header.Topic.Should().Be("test");
            message.Header.MessageType.Should().Be(MessageType.MT_COMMAND);
            message.Header.HandledCount.Should().Be(3);
            message.Header.DelayedMilliseconds.Should().Be(200);
            message.Header.CorrelationId.Should().Be(Guid.Parse("0AF88BBC-07FD-4FC3-9CA7-BF68415A2535"));
            message.Header.ContentType.Should().Be("text/plain");
            message.Header.ReplyTo.Should().Be("reply.queue");
        }
    }
}