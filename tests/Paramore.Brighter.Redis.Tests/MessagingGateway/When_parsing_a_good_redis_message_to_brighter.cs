using System;
using System.Collections.Generic;
using Xunit;
using Paramore.Brighter.MessagingGateway.Redis;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Redis.Tests.MessagingGateway;

[Collection("Redis Shared Pool")]   //shared connection pool so run sequentially
[Trait("Category", "Redis")]
public class RedisGoodMessageParsingTests
{
    private const string GoodMessage =
        "<HEADER\n{\"TimeStamp\":\"2018-02-07T09:38:36Z\",\"Id\":\"18669550-2069-48c5-923d-74a2e79c0748\",\"Topic\":\"test\",\"MessageType\":1,\"Bag\":\"{}\",\"HandledCount\":3,\"DelayedMilliseconds\":200,\"CorrelationId\":\"0AF88BBC-07FD-4FC3-9CA7-BF68415A2535\",\"ContentType\":\"text/plain\",\"ReplyTo\":\"reply.queue\",\"cloudevents:source\":\"http://goparamore.io\",\"cloudevents:type\":\"goparamore.io.Paramore.Brighter.Message\",\"cloudevents:dataschema\":\"http://schema.example.com/test\",\"cloudevents:subject\":\"test-subject\",\"PartitionKey\":\"partition1\",\"cloudevents:traceparent\":\"00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01\",\"cloudevents:tracestate\":\"congo=t61rcWkgMzE\",\"cloudevents:baggage\":\"userId=alice\"}\nHEADER/>\n<BODY\nmore test content\nBODY/>";

    [Fact]
    public void When_parsing_a_good_redis_message_to_brighter()
    {
        var redisMessageCreator = new RedisMessageCreator();

        Message message = redisMessageCreator.CreateMessage(GoodMessage);

        // Assert existing properties
        Assert.Equal(DateTime.Parse("2018-02-07T09:38:36Z"), message.Header.TimeStamp);
        Assert.Equal("test", message.Header.Topic.Value);
        Assert.Equal(MessageType.MT_COMMAND, message.Header.MessageType);
        Assert.Equal(3, message.Header.HandledCount);
        Assert.Equal(TimeSpan.FromMilliseconds(200), message.Header.Delayed);
        Assert.Equal("0AF88BBC-07FD-4FC3-9CA7-BF68415A2535", message.Header.CorrelationId);
        Assert.Equal("text/plain", message.Header.ContentType!.ToString());
        Assert.Equal("reply.queue", message.Header.ReplyTo!.ToString());

        // Assert new Cloud Events properties
        Assert.Equal(new Uri("http://goparamore.io"), message.Header.Source);
        Assert.Equal("goparamore.io.Paramore.Brighter.Message", message.Header.Type);
        Assert.Equal(new Uri("http://schema.example.com/test"), message.Header.DataSchema);
        Assert.Equal("test-subject", message.Header.Subject);
        
        // Assert W3C Trace Context properties
        Assert.Equal(new TraceParent("00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01"), message.Header.TraceParent);
        Assert.Equal(new TraceState("congo=t61rcWkgMzE"), message.Header.TraceState);
        Assert.Contains(new KeyValuePair<string, string?>("userId", "alice"), message.Header.Baggage);
        
        // Assert message body
        Assert.Equal("more test content", message.Body.Value);
    }
}
