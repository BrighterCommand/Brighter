using System;
using System.Linq;
using Xunit;

namespace Paramore.Brighter.Redis.Tests.MessagingGateway.Reactor;

[Collection("Redis Shared Pool")]   //shared connection pool so run sequentially
[Trait("Category", "Redis")]
public class RedisMessageProducerMultipleSendTests : IClassFixture<RedisFixture>
{
    private readonly RedisFixture _redisFixture;
    private readonly Message _messageOne;
    private readonly Message _messageTwo;

    public RedisMessageProducerMultipleSendTests(RedisFixture redisFixture)
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
    public void When_posting_a_message_via_the_messaging_gateway()
    {
        //Need to receive to subscribe to feed, before we send a message. This returns an empty message we discard
        _redisFixture.MessageConsumer.Receive(TimeSpan.FromMilliseconds(1000));

        //Send a sequence of messages, we want to check that ordering is preserved
        _redisFixture.MessageProducer.Send(_messageOne);
        _redisFixture.MessageProducer.Send(_messageTwo);

        //Now receive, and confirm order off is order on
        var sentMessageOne = _redisFixture.MessageConsumer.Receive(TimeSpan.FromMilliseconds(1000)).Single();
        var messageBodyOne = sentMessageOne.Body.Value;
        _redisFixture.MessageConsumer.Acknowledge(sentMessageOne);

        var sentMessageTwo = _redisFixture.MessageConsumer.Receive(TimeSpan.FromMilliseconds(1000)).Single();
        var messageBodyTwo = sentMessageTwo.Body.Value;
        _redisFixture.MessageConsumer.Acknowledge(sentMessageTwo);

        //_should_send_a_message_via_restms_with_the_matching_body
        Assert.Equal(_messageOne.Body.Value, messageBodyOne);
        Assert.Equal(_messageTwo.Body.Value, messageBodyTwo);
    }
}
