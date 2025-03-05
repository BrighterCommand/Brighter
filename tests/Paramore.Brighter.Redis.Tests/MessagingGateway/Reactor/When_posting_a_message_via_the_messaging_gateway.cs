using System;
using System.Linq;
using Xunit;

namespace Paramore.Brighter.Redis.Tests.MessagingGateway.Reactor;

[Collection("Redis Shared Pool")]   //shared connection pool so run sequentially
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
            new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey(topic), MessageType.MT_COMMAND),
            new MessageBody("test content")
        );
    }

    [Fact]
    public void When_posting_a_message_via_the_messaging_gateway()
    {
        _redisFixture.MessageProducer.Send(_message);
        var sentMessage = _redisFixture.MessageConsumer.Receive(TimeSpan.FromMilliseconds(1000)).Single();
        var messageBody = sentMessage.Body.Value;
        _redisFixture.MessageConsumer.Acknowledge(sentMessage);

        Assert.Equal(_message.Body.Value, messageBody);
    }
}
