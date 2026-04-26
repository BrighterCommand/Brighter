using System;
using System.Collections.Generic;
using Paramore.Brighter.MessagingGateway.Redis;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Redis.Tests.MessagingGateway;

[Category("Redis")]
public class RedisGoodMessageParsingTests
{
    private const string GoodMessage =
        "<HEADER\n{\"TimeStamp\":\"2018-02-07T09:38:36Z\",\"Id\":\"18669550-2069-48c5-923d-74a2e79c0748\",\"Topic\":\"test\",\"MessageType\":1,\"Bag\":\"{}\",\"HandledCount\":3,\"DelayedMilliseconds\":200,\"CorrelationId\":\"0AF88BBC-07FD-4FC3-9CA7-BF68415A2535\",\"ContentType\":\"text/plain\",\"ReplyTo\":\"reply.queue\",\"cloudevents:source\":\"http://goparamore.io\",\"cloudevents:type\":\"goparamore.io.Paramore.Brighter.Message\",\"cloudevents:dataschema\":\"http://schema.example.com/test\",\"cloudevents:subject\":\"test-subject\",\"PartitionKey\":\"partition1\",\"cloudevents:traceparent\":\"00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01\",\"cloudevents:tracestate\":\"congo=t61rcWkgMzE\",\"cloudevents:baggage\":\"userId=alice\"}\nHEADER/>\n<BODY\nmore test content\nBODY/>";

    [Test]
    public async Task When_parsing_a_good_redis_message_to_brighter()
    {
        Message message = RedisMessageCreator.CreateMessage(GoodMessage);

        // Assert existing properties
        await Assert.That(message.Header.TimeStamp).IsEqualTo(DateTime.Parse("2018-02-07T09:38:36Z"));
        await Assert.That(message.Header.Topic.Value).IsEqualTo("test");
        await Assert.That(message.Header.MessageType).IsEqualTo(MessageType.MT_COMMAND);
        await Assert.That(message.Header.HandledCount).IsEqualTo(3);
        await Assert.That(message.Header.Delayed).IsEqualTo(TimeSpan.FromMilliseconds(200));
        await Assert.That(message.Header.CorrelationId).IsEqualTo("0AF88BBC-07FD-4FC3-9CA7-BF68415A2535");
        await Assert.That(message.Header.ContentType!.ToString()).IsEqualTo("text/plain");
        await Assert.That(message.Header.ReplyTo!.ToString()).IsEqualTo("reply.queue");

        // Assert new Cloud Events properties
        await Assert.That(message.Header.Source).IsEqualTo(new Uri("http://goparamore.io"));
        await Assert.That(message.Header.Type?.Value).IsEqualTo("goparamore.io.Paramore.Brighter.Message");
        await Assert.That(message.Header.DataSchema).IsEqualTo(new Uri("http://schema.example.com/test"));
        await Assert.That(message.Header.Subject).IsEqualTo("test-subject");
        
        // Assert W3C Trace Context properties
        await Assert.That(message.Header.TraceParent).IsEqualTo(new TraceParent("00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01"));
        await Assert.That(message.Header.TraceState).IsEqualTo(new TraceState("congo=t61rcWkgMzE"));
        await Assert.That(message.Header.Baggage).Contains(new KeyValuePair<string, string?>("userId", "alice"));
        
        // Assert message body
        await Assert.That(message.Body.Value).IsEqualTo("more test content");
    }
}

