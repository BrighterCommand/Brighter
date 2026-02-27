using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Paramore.Brighter.Redis.Tests.MessagingGateway.Proactor;

[Collection("Redis Shared Pool")]   //shared connection pool so run sequentially
[Trait("Category", "Redis")]
public class RedisMessageProducerMultipleSendTestsAsync : IClassFixture<RedisFixture>
{
    private readonly RedisFixture _redisFixture;
    private readonly Message _messageOne;
    private readonly Message _messageTwo;

    public RedisMessageProducerMultipleSendTestsAsync(RedisFixture redisFixture)
    {
        _redisFixture = redisFixture;
        var routingKey = redisFixture.Topic;

        _messageOne = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND),
            new MessageBody("test content")
        );

        _messageTwo = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND),
            new MessageBody("more test content")
        );
    }

    [Fact]
    public async Task When_posting_multiple_messages_via_the_messaging_gateway_async()
    {
        // Need to receive to subscribe to feed, before we send a message. This returns an empty message we discard
        await _redisFixture.MessageConsumer.ReceiveAsync(TimeSpan.FromMilliseconds(1000));

        // Send a sequence of messages, we want to check that ordering is preserved
        await _redisFixture.MessageProducer.SendAsync(_messageOne);
        await _redisFixture.MessageProducer.SendAsync(_messageTwo);

        // Now receive, and confirm order off is order on
        var sentMessageOne = (await _redisFixture.MessageConsumer.ReceiveAsync(TimeSpan.FromMilliseconds(1000))).Single();
        var messageBodyOne = sentMessageOne.Body.Value;
        await _redisFixture.MessageConsumer.AcknowledgeAsync(sentMessageOne);

        var sentMessageTwo = (await _redisFixture.MessageConsumer.ReceiveAsync(TimeSpan.FromMilliseconds(1000))).Single();
        var messageBodyTwo = sentMessageTwo.Body.Value;
        await _redisFixture.MessageConsumer.AcknowledgeAsync(sentMessageTwo);

        // _should_send_a_message_via_restms_with_the_matching_body
        Assert.Equal(_messageOne.Body.Value, messageBodyOne);
        Assert.Equal(_messageTwo.Body.Value, messageBodyTwo);
    }
}
