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

namespace Paramore.Brighter.PostgresSQL.Tests.MessagingGateway;

[Category("PostgresSql")]
public class PostgresMessageConsumerDeliveryErrorDlqTests : IDisposable
{
    private readonly IAmAMessageProducerSync _producer;
    private readonly IAmAMessageConsumerSync _consumer;
    private readonly IAmAMessageConsumerSync _dlqConsumer;
    private readonly Message _message;

    public PostgresMessageConsumerDeliveryErrorDlqTests()
    {
        var testHelper = new PostgresSqlTestHelper();
        testHelper.SetupDatabase();

        var topic = new RoutingKey($"dlq-test-{Guid.NewGuid()}");
        var dlqTopic = new RoutingKey($"dlq-test-dlq-{Guid.NewGuid()}");

        var sub = new PostgresSubscription<MyCommand>(
            new SubscriptionName($"dlq-test-{Guid.NewGuid()}"),
            new ChannelName(topic),
            topic,
            deadLetterRoutingKey: dlqTopic,
            messagePumpType: MessagePumpType.Reactor);

        var dlqSub = new PostgresSubscription<MyCommand>(
            new SubscriptionName($"dlq-consumer-{Guid.NewGuid()}"),
            new ChannelName(dlqTopic),
            dlqTopic,
            messagePumpType: MessagePumpType.Reactor);

        var connection = new PostgresMessagingGatewayConnection(testHelper.Configuration);

        // Producer registry factory ensures queue table exists
        var producerRegistry = new PostgresProducerRegistryFactory(
            connection,
            [new PostgresPublication { Topic = topic }]
        ).Create();

        _producer = (IAmAMessageProducerSync)producerRegistry.LookupBy(topic);

        // Consumer factory creates consumers; table already exists from producer registry
        var consumerFactory = new PostgresConsumerFactory(connection);
        _consumer = consumerFactory.Create(sub);
        _dlqConsumer = consumerFactory.Create(dlqSub);

        _message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), topic, MessageType.MT_COMMAND),
            new MessageBody("test content"));
    }

    [Test]
    public async Task When_rejecting_message_with_delivery_error_should_send_to_dlq()
    {
        // Arrange - send a message and consume it from the source topic
        _producer.Send(_message);
        var receivedMessage = ConsumeMessage(_consumer);
        var originalTopic = receivedMessage.Header.Topic.Value;

        // Act - reject with DeliveryError
        var result = _consumer.Reject(receivedMessage,
            new MessageRejectionReason(RejectionReason.DeliveryError, "Test delivery error"));

        // Assert - reject returns true
        await Assert.That(result).IsTrue();

        // Assert - message should appear on DLQ
        var dlqMessage = ConsumeMessage(_dlqConsumer);
        await Assert.That(dlqMessage.Header.MessageType).IsNotEqualTo(MessageType.MT_NONE);
        await Assert.That(dlqMessage.Body.Value).IsEqualTo(_message.Body.Value);

        // Assert - rejection metadata present in header bag
        await Assert.That(dlqMessage.Header.Bag.ContainsKey("originalTopic")).IsTrue();
        await Assert.That(dlqMessage.Header.Bag["originalTopic"].ToString()).IsEqualTo(originalTopic);
        await Assert.That(dlqMessage.Header.Bag.ContainsKey("rejectionReason")).IsTrue();
        await Assert.That(dlqMessage.Header.Bag["rejectionReason"].ToString()).IsEqualTo(RejectionReason.DeliveryError.ToString());
        await Assert.That(dlqMessage.Header.Bag.ContainsKey("rejectionTimestamp")).IsTrue();
        await Assert.That(dlqMessage.Header.Bag.ContainsKey("originalMessageType")).IsTrue();
        await Assert.That(dlqMessage.Header.Bag["originalMessageType"].ToString()).IsEqualTo(MessageType.MT_COMMAND.ToString());

        // Assert - source message is deleted (re-reading from source returns MT_NONE)
        var sourceMessage = ConsumeMessage(_consumer);
        await Assert.That(sourceMessage.Header.MessageType).IsEqualTo(MessageType.MT_NONE);
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
        ((IDisposable)_producer).Dispose();
    }
}
