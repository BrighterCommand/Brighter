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
public class RedisMessageConsumerDeliveryErrorDlqTests : IDisposable
{
    private readonly RedisMessageProducer _messageProducer;
    private readonly RedisMessageConsumer _consumer;
    private readonly RedisMessageConsumer _dlqConsumer;
    private readonly Message _message;

    public RedisMessageConsumerDeliveryErrorDlqTests()
    {
        var configuration = RedisFixture.RedisMessagingGatewayConfiguration();

        var topic = new RoutingKey($"dlq-test-{Guid.NewGuid()}");
        var dlqTopic = new RoutingKey($"dlq-test-dlq-{Guid.NewGuid()}");
        var queueName = new ChannelName($"dlq-test-{Guid.NewGuid()}");
        var dlqQueueName = new ChannelName($"dlq-test-dlq-{Guid.NewGuid()}");

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
    public void When_rejecting_message_with_delivery_error_should_send_to_dlq()
    {
        //Arrange - subscribe then send
        _consumer.Receive(TimeSpan.FromMilliseconds(1000));
        _dlqConsumer.Receive(TimeSpan.FromMilliseconds(1000)); 
        _messageProducer.Send(_message);
        var receivedMessage = _consumer.Receive(TimeSpan.FromMilliseconds(1000)).Single();

        //Act
        var originalTopic = receivedMessage.Header.Topic.Value;
        _consumer.Reject(receivedMessage,
            new MessageRejectionReason(RejectionReason.DeliveryError, "Test delivery error"));

        //Assert - message should appear on DLQ
        var dlqMessage = _dlqConsumer.Receive(TimeSpan.FromMilliseconds(3000)).Single();

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

    public void Dispose()
    {
        _consumer.Purge();
        _consumer.Dispose();
        _dlqConsumer.Purge();
        _dlqConsumer.Dispose();
        _messageProducer.Dispose();
    }
}
