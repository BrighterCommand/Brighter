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
using Paramore.Brighter.MessagingGateway.Postgres;
using Paramore.Brighter.PostgresSQL.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.MessagingGateway;

[Trait("Category", "PostgresSql")]
public class PostgresMessageConsumerUnacceptableInvalidChannelTests : IDisposable
{
    private readonly IAmAMessageProducerSync _producer;
    private readonly IAmAMessageConsumerSync _consumer;
    private readonly IAmAMessageConsumerSync _dlqConsumer;
    private readonly IAmAMessageConsumerSync _invalidConsumer;
    private readonly Message _message;

    public PostgresMessageConsumerUnacceptableInvalidChannelTests()
    {
        var testHelper = new PostgresSqlTestHelper();
        testHelper.SetupDatabase();

        var topic = new RoutingKey($"invalid-test-{Guid.NewGuid()}");
        var dlqTopic = new RoutingKey($"invalid-test-dlq-{Guid.NewGuid()}");
        var invalidTopic = new RoutingKey($"invalid-test-invalid-{Guid.NewGuid()}");

        var sub = new PostgresSubscription<MyCommand>(
            new SubscriptionName($"invalid-test-{Guid.NewGuid()}"),
            new ChannelName(topic),
            topic,
            deadLetterRoutingKey: dlqTopic,
            invalidMessageRoutingKey: invalidTopic,
            messagePumpType: MessagePumpType.Reactor);

        var connection = new PostgresMessagingGatewayConnection(testHelper.Configuration);

        var producerRegistry = new PostgresProducerRegistryFactory(
            connection,
            [new PostgresPublication { Topic = topic }]
        ).Create();

        _producer = (IAmAMessageProducerSync)producerRegistry.LookupBy(topic);

        var consumerFactory = new PostgresConsumerFactory(connection);
        _consumer = consumerFactory.Create(sub);

        var dlqSub = new PostgresSubscription<MyCommand>(
            new SubscriptionName($"dlq-consumer-{Guid.NewGuid()}"),
            new ChannelName(dlqTopic),
            dlqTopic,
            messagePumpType: MessagePumpType.Reactor);
        _dlqConsumer = consumerFactory.Create(dlqSub);

        var invalidSub = new PostgresSubscription<MyCommand>(
            new SubscriptionName($"invalid-consumer-{Guid.NewGuid()}"),
            new ChannelName(invalidTopic),
            invalidTopic,
            messagePumpType: MessagePumpType.Reactor);
        _invalidConsumer = consumerFactory.Create(invalidSub);

        _message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), topic, MessageType.MT_COMMAND),
            new MessageBody("test content"));
    }

    [Fact]
    public void When_rejecting_message_with_unacceptable_reason_should_send_to_invalid_channel()
    {
        // Arrange - send a message and consume it
        _producer.Send(_message);
        var receivedMessage = ConsumeMessage(_consumer);

        // Act - reject with Unacceptable
        var result = _consumer.Reject(receivedMessage,
            new MessageRejectionReason(RejectionReason.Unacceptable, "Bad message format"));

        // Assert - reject returns true
        Assert.True(result);

        // Assert - message should appear on the invalid message channel
        var invalidMessage = ConsumeMessage(_invalidConsumer);
        Assert.NotEqual(MessageType.MT_NONE, invalidMessage.Header.MessageType);
        Assert.Equal(_message.Body.Value, invalidMessage.Body.Value);

        // Assert - DLQ should be empty
        var dlqMessage = ConsumeMessage(_dlqConsumer);
        Assert.Equal(MessageType.MT_NONE, dlqMessage.Header.MessageType);

        // Assert - source message is deleted
        var sourceMessage = ConsumeMessage(_consumer);
        Assert.Equal(MessageType.MT_NONE, sourceMessage.Header.MessageType);
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
        _invalidConsumer.Purge();
        _invalidConsumer.Dispose();
        ((IDisposable)_producer).Dispose();
    }
}
