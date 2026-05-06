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
using System.Net.Mime;
using System.Text.Json;
using System.Threading.Tasks;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.RocketMQ;
using Paramore.Brighter.RocketMQ.Tests.TestDoubles;
using Paramore.Brighter.RocketMQ.Tests.Utils;

namespace Paramore.Brighter.RocketMQ.Tests.MessagingGateway.Proactor;

[Category("RocketMQ")]
public class RocketMqDeliveryErrorDlqAsyncTests : IAsyncDisposable
{
    private RocketMqMessageProducer _producer;
    private IAmAMessageConsumerAsync _consumer;
    private IAmAMessageConsumerAsync _dlqConsumer;
    private Message _message;

    [Before(Test)]
    public async Task Setup()
    {
        var sourceTopic = new RoutingKey("rmq_dlq_source");
        var dlqTopic = new RoutingKey("rmq_dlq_target");

        var connection = GatewayFactory.CreateConnection();

        // Source topic producer
        var publication = new RocketMqPublication { Topic = sourceTopic };
        _producer = new RocketMqMessageProducer(
            connection,
            await GatewayFactory.CreateProducer(connection, publication),
            publication);

        // Source topic consumer with DLQ routing key (async/Proactor)
        var sourceSub = new RocketMqSubscription<MyCommand>(
            subscriptionName: new SubscriptionName($"async-dlq-test-{Guid.NewGuid()}"),
            channelName: new ChannelName(sourceTopic),
            routingKey: sourceTopic,
            consumerGroup: Guid.NewGuid().ToString(),
            deadLetterRoutingKey: dlqTopic,
            messagePumpType: MessagePumpType.Proactor);

        var consumerFactory = new RocketMessageConsumerFactory(connection);
        _consumer = consumerFactory.CreateAsync(sourceSub);

        // DLQ topic consumer (to verify forwarded messages)
        var dlqSub = new RocketMqSubscription<MyCommand>(
            subscriptionName: new SubscriptionName($"async-dlq-reader-{Guid.NewGuid()}"),
            channelName: new ChannelName(dlqTopic),
            routingKey: dlqTopic,
            consumerGroup: Guid.NewGuid().ToString(),
            messagePumpType: MessagePumpType.Proactor);

        _dlqConsumer = consumerFactory.CreateAsync(dlqSub);

        _message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), sourceTopic, MessageType.MT_COMMAND,
                contentType: new ContentType(MediaTypeNames.Text.Plain)),
            new MessageBody(JsonSerializer.Serialize(
                (object)new MyCommand { Value = "Test Async DLQ" }, JsonSerialisationOptions.Options)));
    }

    [Test]
    public async Task When_rejecting_message_async_with_delivery_error_should_send_to_dlq()
    {
        // Arrange - send a message and consume it from the source topic
        await _consumer.PurgeAsync();
        await _dlqConsumer.PurgeAsync();
        await _producer.SendAsync(_message);
        var receivedMessage = await ConsumeMessageAsync(_consumer);
        var originalTopic = receivedMessage.Header.Topic.Value;

        // Act - reject with DeliveryError (async path)
        var result = await _consumer.RejectAsync(receivedMessage,
            new MessageRejectionReason(RejectionReason.DeliveryError, "Test async delivery error"));

        // Assert - reject returns true
        await Assert.That(result).IsTrue();

        // Assert - message should appear on DLQ
        var dlqMessage = await ConsumeMessageAsync(_dlqConsumer);
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
        } while (maxTries <= 5);

        return new Message();
    }

    public async ValueTask DisposeAsync()
    {
        await _consumer.PurgeAsync();
        await _consumer.DisposeAsync();
        await _dlqConsumer.PurgeAsync();
        await _dlqConsumer.DisposeAsync();
        await _producer.DisposeAsync();
    }
}
