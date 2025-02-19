using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Paramore.Brighter.Redis.Tests.MessagingGateway.Proactor;

[Collection("Redis Shared Pool")]   //shared connection pool so run sequentially
[Trait("Category", "Redis")]
public class RedisMessageProducerSendTestsAsync : IClassFixture<RedisFixture>
{
    private readonly RedisFixture _redisFixture;
    private readonly Message _message;

    public RedisMessageProducerSendTestsAsync(RedisFixture redisFixture)
    {
        const string topic = "test";
        _redisFixture = redisFixture;
        _message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey(topic), MessageType.MT_COMMAND),
            new MessageBody("test content")
        );
    }

    [Fact]
    public async Task When_posting_a_message_via_the_messaging_gateway_async()
    {
        await _redisFixture.MessageConsumer.ReceiveAsync(TimeSpan.FromMilliseconds(1000)); //Need to receive to subscribe to feed, before we send a message. This returns an empty message we discard
        await _redisFixture.MessageProducer.SendAsync(_message);
        var sentMessage = (await _redisFixture.MessageConsumer.ReceiveAsync(TimeSpan.FromMilliseconds(1000))).Single();
        var messageBody = sentMessage.Body.Value;
        await _redisFixture.MessageConsumer.AcknowledgeAsync(sentMessage);

        messageBody.Should().Be(_message.Body.Value);
    }
}
