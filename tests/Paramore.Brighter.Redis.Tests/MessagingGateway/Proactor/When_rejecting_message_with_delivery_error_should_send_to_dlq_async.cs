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
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.Redis;
using Xunit;

namespace Paramore.Brighter.Redis.Tests.MessagingGateway.Proactor;

[Collection("Redis Shared Pool")]
[Trait("Category", "Redis")]
[Trait("Fragile", "CI")]
public class RedisMessageConsumerDeliveryErrorDlqAsyncTests : IAsyncDisposable
{
    private readonly RedisMessageProducer _messageProducer;
    private readonly RedisMessageConsumer _consumer;
    private readonly RedisMessageConsumer _dlqConsumer;
    private readonly Message _message;

    public RedisMessageConsumerDeliveryErrorDlqAsyncTests()
    {
        var configuration = RedisFixture.RedisMessagingGatewayConfiguration();

        var topic = new RoutingKey($"dlq-async-test-{Guid.NewGuid()}");
        var dlqTopic = new RoutingKey($"dlq-async-test-dlq-{Guid.NewGuid()}");
        var queueName = new ChannelName($"dlq-async-test-{Guid.NewGuid()}");
        var dlqQueueName = new ChannelName($"dlq-async-test-dlq-{Guid.NewGuid()}");

        _messageProducer = new RedisMessageProducer(configuration,
            new RedisMessagePublication { Topic = topic });

        _consumer = new RedisMessageConsumer(configuration, queueName, topic,
            deadLetterRoutingKey: dlqTopic);

        _dlqConsumer = new RedisMessageConsumer(configuration, dlqQueueName, dlqTopic);

        _message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), topic, MessageType.MT_COMMAND),
            new MessageBody("test content"));
    }

    [Fact]
    public async Task When_rejecting_message_with_delivery_error_should_send_to_dlq_async()
    {
        //Arrange - subscribe then send
        await _consumer.ReceiveAsync(TimeSpan.FromMilliseconds(1000));
        await _dlqConsumer.ReceiveAsync(TimeSpan.FromMilliseconds(1000));
        await _messageProducer.SendAsync(_message);
        var receivedMessage = (await _consumer.ReceiveAsync(TimeSpan.FromMilliseconds(1000))).Single();

        //Act
        var originalTopic = receivedMessage.Header.Topic.Value;
        await _consumer.RejectAsync(receivedMessage,
            new MessageRejectionReason(RejectionReason.DeliveryError, "Test delivery error"));

        //Assert - message should appear on DLQ
        var dlqMessage = (await _dlqConsumer.ReceiveAsync(TimeSpan.FromMilliseconds(3000))).Single();

        Assert.NotEqual(MessageType.MT_NONE, dlqMessage.Header.MessageType);
        Assert.Equal(_message.Body.Value, dlqMessage.Body.Value);

        // Verify rejection metadata
        Assert.True(dlqMessage.Header.Bag.ContainsKey("originalTopic"));
        Assert.Equal(originalTopic, dlqMessage.Header.Bag["originalTopic"].ToString());
        Assert.True(dlqMessage.Header.Bag.ContainsKey("rejectionReason"));
        Assert.Equal(RejectionReason.DeliveryError.ToString(),
            dlqMessage.Header.Bag["rejectionReason"].ToString());
        Assert.True(dlqMessage.Header.Bag.ContainsKey("rejectionTimestamp"));
        Assert.True(dlqMessage.Header.Bag.ContainsKey("originalMessageType"));
        Assert.Equal(MessageType.MT_COMMAND.ToString(),
            dlqMessage.Header.Bag["originalMessageType"].ToString());
    }

    public async ValueTask DisposeAsync()
    {
        await _consumer.PurgeAsync();
        await _consumer.DisposeAsync();
        await _dlqConsumer.PurgeAsync();
        await _dlqConsumer.DisposeAsync();
        await _messageProducer.DisposeAsync();
    }
}
