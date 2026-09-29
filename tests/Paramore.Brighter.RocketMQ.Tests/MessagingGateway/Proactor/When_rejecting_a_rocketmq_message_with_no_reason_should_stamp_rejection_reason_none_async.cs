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
using System.Diagnostics;
using System.Net.Mime;
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.RocketMQ;
using Paramore.Brighter.RocketMQ.Tests.TestDoubles;
using Paramore.Brighter.RocketMQ.Tests.Utils;

namespace Paramore.Brighter.RocketMQ.Tests.MessagingGateway.Proactor;

/// <summary>
/// Task 7.2 — R-28 edge case 1; ADR 0077 null-reason decision. A Reject with no
/// <see cref="MessageRejectionReason"/> must still land on the dead-letter queue, stamped
/// <c>rejectionReason = "None"</c> and with no <c>rejectionMessage</c> (a null reason carries no
/// description).
/// </summary>
/// <remarks>
/// Uses the pre-created <c>rmq_rej_none_p</c> / <c>rmq_rej_none_p_DLQ</c> topics (the C# client
/// never auto-creates one) with a fresh consumer group per run, and picks its own message out by
/// id from both the source and DLQ topics, acknowledging anything stale, because a fresh consumer
/// group can be handed messages earlier runs left on the topic.
/// </remarks>
[Property("Category", "RocketMQ")]
public class RocketMqRejectWithNoReasonTestsAsync
{
    private const string Topic = "rmq_rej_none_p";
    private const string DlqTopic = "rmq_rej_none_p_DLQ";
    private static readonly TimeSpan s_receiveMessageTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan s_receiveCeiling = TimeSpan.FromSeconds(30);

    private RocketMessagingGatewayConnection? _connection;
    private RocketMqMessageProducer? _producer;
    private IAmAMessageConsumerAsync? _consumer;

    [Before(HookType.Test)]public async Task InitializeAsync()
    {
        _connection = GatewayFactory.CreateConnection();
        var routingKey = new RoutingKey(Topic);
        var publication = new RocketMqPublication { Topic = routingKey };

        _producer = new RocketMqMessageProducer(
            _connection,
            await GatewayFactory.CreateProducer(_connection, publication),
            publication);

        var subscription = new RocketMqSubscription<MyCommand>(
            subscriptionName: new SubscriptionName($"reject-none-{Guid.NewGuid():N}"),
            channelName: new ChannelName(Topic),
            routingKey: routingKey,
            consumerGroup: $"reject-none-{Guid.NewGuid():N}",
            messagePumpType: MessagePumpType.Proactor,
            receiveMessageTimeout: s_receiveMessageTimeout,
            deadLetterRoutingKey: new RoutingKey(DlqTopic));

        _consumer = new RocketMessageConsumerFactory(_connection).CreateAsync(subscription);
    }

    [After(HookType.Test)]public async Task DisposeAsync()
    {
        if (_consumer != null)
            await _consumer.DisposeAsync();

        if (_producer != null)
            await _producer.DisposeAsync();
    }

    [Test]
    public async Task When_rejecting_a_rocketmq_message_with_no_reason_should_stamp_rejection_reason_none_async()
    {
        // Arrange
        var message = CreateMessage(new RoutingKey(Topic));
        await _producer!.SendAsync(message);

        var received = await ReceiveOursAsync(_consumer!, message.Id, s_receiveCeiling);
        await Assert.That(received).IsNotNull();
        var originalTopic = received!.Header.Topic.Value;

        // Act
        await _consumer!.RejectAsync(received, null);

        // Assert — the message reaches the dead-letter queue stamped rejectionReason="None"
        await using var dlqSimpleConsumer = await GatewayFactory.CreateSimpleConsumer(_connection!, DlqTopic);
        await using var dlqConsumer = new RocketMessageConsumer(dlqSimpleConsumer, 1, TimeSpan.FromSeconds(30));

        var dlqMessage = await ReceiveOursAsync(dlqConsumer, message.Id, s_receiveCeiling);
        await Assert.That(dlqMessage).IsNotNull();
        await dlqConsumer.AcknowledgeAsync(dlqMessage!);

        await Assert.That(dlqMessage!.Header.Bag.ContainsKey("rejectionReason")).IsTrue();
        await Assert.That(dlqMessage.Header.Bag["rejectionReason"].ToString()).IsEqualTo(RejectionReason.None.ToString());

        // verify rejectionMessage is absent (null reason carries no description)
        await Assert.That(dlqMessage.Header.Bag.ContainsKey("rejectionMessage")).IsFalse();

        // verify other metadata is present
        await Assert.That(dlqMessage.Header.Bag.ContainsKey("originalTopic")).IsTrue();
        await Assert.That(dlqMessage.Header.Bag["originalTopic"].ToString()).IsEqualTo(originalTopic);
        await Assert.That(dlqMessage.Header.Bag.ContainsKey("originalMessageType")).IsTrue();
        await Assert.That(dlqMessage.Header.Bag["originalMessageType"].ToString()).IsEqualTo(MessageType.MT_COMMAND.ToString());
        await Assert.That(dlqMessage.Header.Bag.ContainsKey("rejectionTimestamp")).IsTrue();

        await Assert.That(dlqMessage.Body.Value).IsEqualTo(message.Body.Value);
    }

    private static Message CreateMessage(RoutingKey routingKey) =>
        new(
            new MessageHeader(Id.Random(), routingKey, MessageType.MT_COMMAND,
                contentType: new ContentType(MediaTypeNames.Text.Plain)),
            new MessageBody("reject with no reason"));

    private static async Task<Message?> ReceiveOursAsync(IAmAMessageConsumerAsync consumer, Id messageId, TimeSpan ceiling)
    {
        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < ceiling)
        {
            foreach (var received in await consumer.ReceiveAsync(s_receiveMessageTimeout))
            {
                if (received.Header.MessageType == MessageType.MT_NONE)
                    continue;
                if (received.Id == messageId)
                    return received;

                await consumer.AcknowledgeAsync(received);
            }
        }

        return null;
    }
}
