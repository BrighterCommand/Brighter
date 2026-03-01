using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Paramore.Brighter.Redis.Tests.MessagingGateway.Proactor;

[Collection("Redis Shared Pool")]   //shared connection pool so run sequentially
[Trait("Category", "Redis")]
public class RedisRequeueMessageTestsAsync : IClassFixture<RedisFixture>, IAsyncDisposable
{
    private readonly RedisFixture _redisFixture;
    private readonly Message _messageOne;
    private readonly Message _messageTwo;

    public RedisRequeueMessageTestsAsync(RedisFixture redisFixture)
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
    public async Task When_requeing_a_failed_message_async()
    {
        // Need to receive to subscribe to feed, before we send a message. This returns an empty message we discard
        await _redisFixture.MessageConsumer.ReceiveAsync(TimeSpan.FromMilliseconds(1000));

        // Send a sequence of messages, we want to check that ordering is preserved
        await _redisFixture.MessageProducer.SendAsync(_messageOne);
        await _redisFixture.MessageProducer.SendAsync(_messageTwo);

        // Now receive, the first message
        var sentMessageOne = (await _redisFixture.MessageConsumer.ReceiveAsync(TimeSpan.FromMilliseconds(1000))).Single();

        // Now requeue the first message
        await _redisFixture.MessageConsumer.RequeueAsync(_messageOne);

        // Try receiving again; messageTwo should come first
        var sentMessageTwo = (await _redisFixture.MessageConsumer.ReceiveAsync(TimeSpan.FromMilliseconds(1000))).Single();
        var messageBodyTwo = sentMessageTwo.Body.Value;
        await _redisFixture.MessageConsumer.AcknowledgeAsync(sentMessageTwo);

        sentMessageOne = (await _redisFixture.MessageConsumer.ReceiveAsync(TimeSpan.FromMilliseconds(1000))).Single();
        var messageBodyOne = sentMessageOne.Body.Value;
        await _redisFixture.MessageConsumer.AcknowledgeAsync(sentMessageOne);

        // _should_send_a_message_via_restms_with_the_matching_body
        Assert.Equal(_messageOne.Body.Value, messageBodyOne);
        Assert.Equal(_messageTwo.Body.Value, messageBodyTwo);
    }

    public async ValueTask DisposeAsync()
    {
        await _redisFixture.MessageConsumer.DisposeAsync();
        await _redisFixture.MessageProducer.DisposeAsync();
    }
}
