using System;
using Xunit;
using Paramore.Brighter.MessagingGateway.Redis;

namespace Paramore.Brighter.Redis.Tests.MessagingGateway;

[Collection("Redis Shared Pool")]   //shared connection pool so run sequentially
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

        Assert.Equal(DateTime.Parse("2018-02-07T09:38:36Z"), message.Header.TimeStamp);
        Assert.Equal("test", message.Header.Topic.Value);
        Assert.Equal(MessageType.MT_COMMAND, message.Header.MessageType);
        Assert.Equal(3, message.Header.HandledCount);
        Assert.Equal(TimeSpan.FromMilliseconds(200), message.Header.Delayed);
        Assert.Equal("0AF88BBC-07FD-4FC3-9CA7-BF68415A2535", message.Header.CorrelationId);
        Assert.Equal("text/plain", message.Header.ContentType);
        Assert.Equal("reply.queue", message.Header.ReplyTo);
    }
}
