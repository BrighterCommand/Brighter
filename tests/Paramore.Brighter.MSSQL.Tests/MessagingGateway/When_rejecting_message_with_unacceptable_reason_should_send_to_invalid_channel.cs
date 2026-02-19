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
public class MsSqlMessageConsumerUnacceptableInvalidChannelTests : IDisposable
{
    private readonly MsSqlMessageProducer _producer;
    private readonly MsSqlMessageConsumer _consumer;
    private readonly MsSqlMessageConsumer _invalidConsumer;
    private readonly MsSqlMessageConsumer _dlqConsumer;
    private readonly Message _message;

    public MsSqlMessageConsumerUnacceptableInvalidChannelTests()
    {
        var testHelper = new MsSqlTestHelper();
        testHelper.SetupQueueDb();

        var topic = new RoutingKey($"invalid-test-{Guid.NewGuid()}");
        var dlqTopic = new RoutingKey($"invalid-test-dlq-{Guid.NewGuid()}");
        var invalidTopic = new RoutingKey($"invalid-test-invalid-{Guid.NewGuid()}");

        var sub = new MsSqlSubscription<MyCommand>(
            new SubscriptionName($"invalid-test-{Guid.NewGuid()}"),
            new ChannelName(topic),
            topic,
            deadLetterRoutingKey: dlqTopic,
            invalidMessageRoutingKey: invalidTopic,
            messagePumpType: MessagePumpType.Reactor);

        _producer = new MsSqlMessageProducer(testHelper.QueueConfiguration);

        _consumer = (MsSqlMessageConsumer)new MsSqlMessageConsumerFactory(testHelper.QueueConfiguration).Create(sub);

        _invalidConsumer = new MsSqlMessageConsumer(testHelper.QueueConfiguration, invalidTopic);
        _dlqConsumer = new MsSqlMessageConsumer(testHelper.QueueConfiguration, dlqTopic);

        _message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), topic, MessageType.MT_COMMAND),
            new MessageBody("test content"));
    }

    [Fact]
    public void When_rejecting_message_with_unacceptable_reason_should_send_to_invalid_channel()
    {
        // Arrange - send a message and consume it from the source topic
        _producer.Send(_message);
        var receivedMessage = ConsumeMessage(_consumer);

        // Act - reject with Unacceptable reason
        var result = _consumer.Reject(receivedMessage,
            new MessageRejectionReason(RejectionReason.Unacceptable, "Bad message format"));

        // Assert - reject returns true
        Assert.True(result);

        // Assert - message should appear on invalid message channel
        var invalidMessage = ConsumeMessage(_invalidConsumer);
        Assert.NotEqual(MessageType.MT_NONE, invalidMessage.Header.MessageType);
        Assert.Equal(_message.Body.Value, invalidMessage.Body.Value);
        Assert.Equal(RejectionReason.Unacceptable.ToString(),
            invalidMessage.Header.Bag["rejectionReason"].ToString());

        // Assert - DLQ should be empty
        var dlqMessage = _dlqConsumer.Receive(TimeSpan.FromMilliseconds(1000)).First();
        Assert.Equal(MessageType.MT_NONE, dlqMessage.Header.MessageType);
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
        _invalidConsumer.Purge();
        _invalidConsumer.Dispose();
        _dlqConsumer.Purge();
        _dlqConsumer.Dispose();
        _producer.Dispose();
    }
}
