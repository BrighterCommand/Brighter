#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Linq;
using Paramore.Brighter.MessagingGateway.Redis;
using Xunit;

namespace Paramore.Brighter.Redis.Tests.MessagingGateway.Reactor;

[Collection("Redis Shared Pool")]
[Trait("Category", "Redis")]
[Trait("Fragile", "CI")]
public class RedisMessageConsumerUnacceptableInvalidChannelTests : IDisposable
{
    private readonly RedisMessageProducer _messageProducer;
    private readonly RedisMessageConsumer _consumer;
    private readonly RedisMessageConsumer _invalidConsumer;
    private readonly RedisMessageConsumer _dlqConsumer;
    private readonly Message _message;

    public RedisMessageConsumerUnacceptableInvalidChannelTests()
    {
        var configuration = RedisFixture.RedisMessagingGatewayConfiguration();

        var topic = new RoutingKey($"invalid-test-{Guid.NewGuid()}");
        var dlqTopic = new RoutingKey($"invalid-test-dlq-{Guid.NewGuid()}");
        var invalidTopic = new RoutingKey($"invalid-test-invalid-{Guid.NewGuid()}");
        var queueName = new ChannelName($"invalid-test-{Guid.NewGuid()}");
        var dlqQueueName = new ChannelName($"invalid-test-dlq-{Guid.NewGuid()}");
        var invalidQueueName = new ChannelName($"invalid-test-invalid-{Guid.NewGuid()}");

        _messageProducer = new RedisMessageProducer(configuration,
            new RedisMessagePublication { Topic = topic });

        _consumer = new RedisMessageConsumer(configuration, queueName, topic,
            deadLetterRoutingKey: dlqTopic,
            invalidMessageRoutingKey: invalidTopic);

        _dlqConsumer = new RedisMessageConsumer(configuration, dlqQueueName, dlqTopic);
        _invalidConsumer = new RedisMessageConsumer(configuration, invalidQueueName, invalidTopic);

        _message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), topic, MessageType.MT_COMMAND),
            new MessageBody("test content"));
    }

    [Fact]
    public void When_rejecting_message_with_unacceptable_reason_should_send_to_invalid_channel()
    {
        //Arrange - subscribe then send
        _consumer.Receive(TimeSpan.FromMilliseconds(1000));
        _dlqConsumer.Receive(TimeSpan.FromMilliseconds(1000));
        _invalidConsumer.Receive(TimeSpan.FromMilliseconds(1000));
        _messageProducer.Send(_message);
        var receivedMessage = _consumer.Receive(TimeSpan.FromMilliseconds(1000)).Single();

        //Act
        _consumer.Reject(receivedMessage,
            new MessageRejectionReason(RejectionReason.Unacceptable, "Bad message format"));

        //Assert - message should appear on invalid channel, not on DLQ
        var invalidMessage = _invalidConsumer.Receive(TimeSpan.FromMilliseconds(3000)).Single();

        Assert.NotEqual(MessageType.MT_NONE, invalidMessage.Header.MessageType);
        Assert.Equal(_message.Body.Value, invalidMessage.Body.Value);
        Assert.Equal(RejectionReason.Unacceptable.ToString(),
            invalidMessage.Header.Bag["rejectionReason"].ToString());

        // DLQ should be empty
        var dlqMessages = _dlqConsumer.Receive(TimeSpan.FromMilliseconds(1000));
        Assert.Empty(dlqMessages);
    }

    public void Dispose()
    {
        _consumer.Purge();
        _consumer.Dispose();
        _dlqConsumer.Purge();
        _dlqConsumer.Dispose();
        _invalidConsumer.Purge();
        _invalidConsumer.Dispose();
        _messageProducer.Dispose();
    }
}
