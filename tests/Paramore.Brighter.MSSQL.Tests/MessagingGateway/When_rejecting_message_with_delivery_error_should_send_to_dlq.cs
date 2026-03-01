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
using Paramore.Brighter.MessagingGateway.MsSql;
using Paramore.Brighter.MSSQL.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.MessagingGateway;

[Trait("Category", "MSSQL")]
public class MsSqlMessageConsumerDeliveryErrorDlqTests : IDisposable
{
    private readonly MsSqlMessageProducer _producer;
    private readonly MsSqlMessageConsumer _consumer;
    private readonly MsSqlMessageConsumer _dlqConsumer;
    private readonly Message _message;

    public MsSqlMessageConsumerDeliveryErrorDlqTests()
    {
        var testHelper = new MsSqlTestHelper();
        testHelper.SetupQueueDb();

        var topic = new RoutingKey($"dlq-test-{Guid.NewGuid()}");
        var dlqTopic = new RoutingKey($"dlq-test-dlq-{Guid.NewGuid()}");

        var sub = new MsSqlSubscription<MyCommand>(
            new SubscriptionName($"dlq-test-{Guid.NewGuid()}"),
            new ChannelName(topic),
            topic,
            deadLetterRoutingKey: dlqTopic,
            messagePumpType: MessagePumpType.Reactor);

        _producer = new MsSqlMessageProducer(testHelper.QueueConfiguration);

        _consumer = (MsSqlMessageConsumer)new MsSqlMessageConsumerFactory(testHelper.QueueConfiguration).Create(sub);

        _dlqConsumer = new MsSqlMessageConsumer(testHelper.QueueConfiguration, dlqTopic);

        _message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), topic, MessageType.MT_COMMAND),
            new MessageBody("test content"));
    }

    [Fact]
    public void When_rejecting_message_with_delivery_error_should_send_to_dlq()
    {
        // Arrange - send a message and consume it from the source topic
        _producer.Send(_message);
        var receivedMessage = ConsumeMessage(_consumer);
        var originalTopic = receivedMessage.Header.Topic.Value;

        // Act - reject with DeliveryError
        var result = _consumer.Reject(receivedMessage,
            new MessageRejectionReason(RejectionReason.DeliveryError, "Test delivery error"));

        // Assert - reject returns true
        Assert.True(result);

        // Assert - message should appear on DLQ
        var dlqMessage = ConsumeMessage(_dlqConsumer);
        Assert.NotEqual(MessageType.MT_NONE, dlqMessage.Header.MessageType);
        Assert.Equal(_message.Body.Value, dlqMessage.Body.Value);

        // Assert - rejection metadata present in header bag
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

    private static Message ConsumeMessage(IAmAMessageConsumerSync consumer)
    {
        var maxTries = 0;
        do
        {
            var messages = consumer.Receive(TimeSpan.FromMilliseconds(1000));
            var message = messages.First();
            if (message.Header.MessageType != MessageType.MT_NONE)
                return message;
            maxTries++;
        } while (maxTries <= 3);

        return new Message();
    }

    public void Dispose()
    {
        _consumer.Purge();
        _consumer.Dispose();
        _dlqConsumer.Purge();
        _dlqConsumer.Dispose();
        _producer.Dispose();
    }
}
