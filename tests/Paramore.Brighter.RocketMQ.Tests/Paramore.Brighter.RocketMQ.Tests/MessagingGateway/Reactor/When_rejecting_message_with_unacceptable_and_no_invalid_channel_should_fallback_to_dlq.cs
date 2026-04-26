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
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.RocketMQ;
using Paramore.Brighter.RocketMQ.Tests.TestDoubles;
using Paramore.Brighter.RocketMQ.Tests.Utils;

namespace Paramore.Brighter.RocketMQ.Tests.MessagingGateway.Reactor;

[Category("RocketMQ")]
public class RocketMqUnacceptableFallbackToDlqTests : IDisposable
{
    private RocketMqMessageProducer _producer;
    private IAmAMessageConsumerSync _consumer;
    private IAmAMessageConsumerSync _dlqConsumer;
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

        // Source topic consumer with DLQ only (no invalid message routing key)
        var sourceSub = new RocketMqSubscription<MyCommand>(
            subscriptionName: new SubscriptionName($"fallback-test-{Guid.NewGuid()}"),
            channelName: new ChannelName(sourceTopic),
            routingKey: sourceTopic,
            consumerGroup: Guid.NewGuid().ToString(),
            deadLetterRoutingKey: dlqTopic,
            messagePumpType: MessagePumpType.Reactor);

        var consumerFactory = new RocketMessageConsumerFactory(connection);
        _consumer = consumerFactory.Create(sourceSub);

        // DLQ topic consumer (to verify fallback routing)
        var dlqSub = new RocketMqSubscription<MyCommand>(
            subscriptionName: new SubscriptionName($"dlq-reader-{Guid.NewGuid()}"),
            channelName: new ChannelName(dlqTopic),
            routingKey: dlqTopic,
            consumerGroup: Guid.NewGuid().ToString(),
            messagePumpType: MessagePumpType.Reactor);

        _dlqConsumer = consumerFactory.Create(dlqSub);

        _message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), sourceTopic, MessageType.MT_COMMAND,
                contentType: new ContentType(MediaTypeNames.Text.Plain)),
            new MessageBody(JsonSerializer.Serialize(
                (object)new MyCommand { Value = "Test Fallback" }, JsonSerialisationOptions.Options)));
    }

    [Test]
    public async Task When_rejecting_message_with_unacceptable_and_no_invalid_channel_should_fallback_to_dlq()
    {
        // Arrange - send a message and consume it from the source topic
        _consumer.Purge();
        _dlqConsumer.Purge();
        await _producer.SendAsync(_message);
        var receivedMessage = ConsumeMessage(_consumer);

        // Act - reject with Unacceptable reason (no invalid channel configured, should fall back to DLQ)
        var result = _consumer.Reject(receivedMessage,
            new MessageRejectionReason(RejectionReason.Unacceptable, "Message failed validation"));

        // Assert - reject returns true
        await Assert.That(result).IsTrue();

        // Assert - message should appear on DLQ (fallback)
        var dlqMessage = ConsumeMessage(_dlqConsumer);
        await Assert.That(dlqMessage.Header.MessageType).IsNotEqualTo(MessageType.MT_NONE);
        await Assert.That(dlqMessage.Body.Value).IsEqualTo(_message.Body.Value);

        // Assert - rejection metadata present
        await Assert.That(dlqMessage.Header.Bag.ContainsKey("originalTopic")).IsTrue();
        await Assert.That(dlqMessage.Header.Bag.ContainsKey("rejectionReason")).IsTrue();
        await Assert.That(dlqMessage.Header.Bag["rejectionReason"].ToString()).IsEqualTo(RejectionReason.Unacceptable.ToString());
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
        } while (maxTries <= 5);

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
