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
using Paramore.Brighter.MessagingGateway.Postgres;
using Paramore.Brighter.PostgresSQL.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.MessagingGateway;

[Trait("Category", "PostgresSql")]
public class PostgresMessageConsumerDeliveryErrorDlqAsyncTests : IAsyncDisposable
{
    private readonly IAmAMessageProducerAsync _producer;
    private readonly IAmAMessageConsumerAsync _consumer;
    private readonly IAmAMessageConsumerAsync _dlqConsumer;
    private readonly Message _message;

    public PostgresMessageConsumerDeliveryErrorDlqAsyncTests()
    {
        var testHelper = new PostgresSqlTestHelper();
        testHelper.SetupDatabase();

        var topic = new RoutingKey($"dlq-async-test-{Guid.NewGuid()}");
        var dlqTopic = new RoutingKey($"dlq-async-test-dlq-{Guid.NewGuid()}");

        var sub = new PostgresSubscription<MyCommand>(
            new SubscriptionName($"dlq-async-test-{Guid.NewGuid()}"),
            new ChannelName(topic),
            topic,
            deadLetterRoutingKey: dlqTopic,
            messagePumpType: MessagePumpType.Proactor);

        var connection = new PostgresMessagingGatewayConnection(testHelper.Configuration);

        var producerRegistry = new PostgresProducerRegistryFactory(
            connection,
            [new PostgresPublication { Topic = topic }]
        ).Create();

        _producer = (IAmAMessageProducerAsync)producerRegistry.LookupBy(topic);

        var consumerFactory = new PostgresConsumerFactory(connection);
        _consumer = consumerFactory.CreateAsync(sub);

        var dlqSub = new PostgresSubscription<MyCommand>(
            new SubscriptionName($"dlq-async-consumer-{Guid.NewGuid()}"),
            new ChannelName(dlqTopic),
            dlqTopic,
            messagePumpType: MessagePumpType.Proactor);
        _dlqConsumer = consumerFactory.CreateAsync(dlqSub);

        _message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), topic, MessageType.MT_COMMAND),
            new MessageBody("test content"));
    }

    [Fact]
    public async Task When_rejecting_message_with_delivery_error_should_send_to_dlq_async()
    {
        // Arrange - send a message and consume it from the source topic via async path
        await _producer.SendAsync(_message);
        var receivedMessage = await ConsumeMessageAsync(_consumer);
        var originalTopic = receivedMessage.Header.Topic.Value;

        // Act - reject with DeliveryError via async path
        var result = await _consumer.RejectAsync(receivedMessage,
            new MessageRejectionReason(RejectionReason.DeliveryError, "Test delivery error"));

        // Assert - reject returns true
        Assert.True(result);

        // Assert - message should appear on DLQ
        var dlqMessage = await ConsumeMessageAsync(_dlqConsumer);
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

        // Assert - source message is deleted (re-reading from source returns MT_NONE)
        var sourceMessage = await ConsumeMessageAsync(_consumer);
        Assert.Equal(MessageType.MT_NONE, sourceMessage.Header.MessageType);
    }

    private static async Task<Message> ConsumeMessageAsync(IAmAMessageConsumerAsync consumer)
    {
        var maxTries = 0;
        do
        {
            var messages = await consumer.ReceiveAsync(TimeSpan.FromMilliseconds(1000));
            var message = messages.First();
            if (message.Header.MessageType != MessageType.MT_NONE)
                return message;
            maxTries++;
        } while (maxTries <= 3);

        return new Message();
    }

    public async ValueTask DisposeAsync()
    {
        await _consumer.PurgeAsync();
        await _consumer.DisposeAsync();
        await _dlqConsumer.PurgeAsync();
        await _dlqConsumer.DisposeAsync();
        ((IDisposable)_producer).Dispose();
        GC.SuppressFinalize(this);
    }
}
