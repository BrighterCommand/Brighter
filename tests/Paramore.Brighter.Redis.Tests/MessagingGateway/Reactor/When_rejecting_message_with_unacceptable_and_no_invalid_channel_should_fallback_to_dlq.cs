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

namespace Paramore.Brighter.Redis.Tests.MessagingGateway.Reactor;

[Category("Redis")]
[Property("Fragile", "CI")]
public class RedisMessageConsumerUnacceptableFallbackToDlqTests : IDisposable
{
    private readonly RedisMessageProducer _messageProducer;
    private readonly RedisMessageConsumer _consumer;
    private readonly RedisMessageConsumer _dlqConsumer;
    private readonly Message _message;

    public RedisMessageConsumerUnacceptableFallbackToDlqTests()
    {
        var configuration = RedisFixture.RedisMessagingGatewayConfiguration();

        var topic = new RoutingKey($"fallback-test-{Guid.NewGuid()}");
        var dlqTopic = new RoutingKey($"fallback-test-dlq-{Guid.NewGuid()}");
        var queueName = new ChannelName($"fallback-test-{Guid.NewGuid()}");
        var dlqQueueName = new ChannelName($"fallback-test-dlq-{Guid.NewGuid()}");

        _messageProducer = new RedisMessageProducer(configuration,
            new RedisMessagePublication { Topic = topic });

        // Only DLQ configured — no invalidMessageRoutingKey
        _consumer = new RedisMessageConsumer(configuration, queueName, topic,
            deadLetterRoutingKey: dlqTopic);

        _dlqConsumer = new RedisMessageConsumer(configuration, dlqQueueName, dlqTopic);

        _message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), topic, MessageType.MT_COMMAND),
            new MessageBody("test content"));
    }

    [Test]
    public async Task When_rejecting_message_with_unacceptable_and_no_invalid_channel_should_fallback_to_dlq()
    {
        //Arrange - subscribe then send
        await _consumer.ReceiveAsync(TimeSpan.FromMilliseconds(1000));
        await _dlqConsumer.ReceiveAsync(TimeSpan.FromMilliseconds(1000));
        await _messageProducer.SendAsync(_message);
        var receivedMessage = (await _consumer.ReceiveAsync(TimeSpan.FromMilliseconds(1000))).Single();

        //Act - reject with Unacceptable, but no invalid channel configured
        await _consumer.RejectAsync(receivedMessage,
            new MessageRejectionReason(RejectionReason.Unacceptable, "Bad message format"));

        //Assert - message should fall back to DLQ
        var dlqMessage = (await _dlqConsumer.ReceiveAsync(TimeSpan.FromMilliseconds(3000))).Single();

        await Assert.That(dlqMessage.Header.MessageType).IsNotEqualTo(MessageType.MT_NONE);
        await Assert.That(dlqMessage.Body.Value).IsEqualTo(_message.Body.Value);
        await Assert.That(dlqMessage.Header.Bag["rejectionReason"].ToString()).IsEqualTo(RejectionReason.Unacceptable.ToString());
    }

    public void Dispose()
    {
        _consumer.Purge();
        _dlqConsumer.Purge();
        _consumer.Dispose();
        _dlqConsumer.Dispose();
        _messageProducer.Dispose();
    }
}

