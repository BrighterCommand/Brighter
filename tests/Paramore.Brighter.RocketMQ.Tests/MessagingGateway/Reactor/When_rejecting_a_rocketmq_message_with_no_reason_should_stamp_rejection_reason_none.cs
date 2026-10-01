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
using Paramore.Brighter.MessagingGateway.RocketMQ;
using Paramore.Brighter.RocketMQ.Tests.TestDoubles;
using Paramore.Brighter.RocketMQ.Tests.Utils;
using Xunit;

namespace Paramore.Brighter.RocketMQ.Tests.MessagingGateway.Reactor;

/// <summary>
/// Task 7.2 — R-28 edge case 1; ADR 0077 null-reason decision. A Reject with no
/// <see cref="MessageRejectionReason"/> must still land on the dead-letter queue, stamped
/// <c>rejectionReason = "None"</c> and with no <c>rejectionMessage</c> (a null reason carries no
/// description).
/// </summary>
/// <remarks>
/// Uses the pre-created <c>rmq_rej_none_r</c> / <c>rmq_rej_none_r_DLQ</c> topics (the C# client
/// never auto-creates one) with a fresh consumer group per run, and picks its own message out by
/// id from both the source and DLQ topics, acknowledging anything stale, because a fresh consumer
/// group can be handed messages earlier runs left on the topic.
/// </remarks>
[Trait("Category", "RocketMQ")]
public class RocketMqRejectWithNoReasonTests : IDisposable
{
    private const string Topic = "rmq_rej_none_r";
    private const string DlqTopic = "rmq_rej_none_r_DLQ";
    private static readonly TimeSpan s_receiveMessageTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan s_receiveCeiling = TimeSpan.FromSeconds(30);

    private readonly RocketMessagingGatewayConnection _connection;
    private readonly RocketMqMessageProducer _producer;
    private readonly IAmAMessageConsumerSync _consumer;

    public RocketMqRejectWithNoReasonTests()
    {
        _connection = GatewayFactory.CreateConnection();
        var routingKey = new RoutingKey(Topic);
        var publication = new RocketMqPublication { Topic = routingKey };

        _producer = new RocketMqMessageProducer(
            _connection,
            GatewayFactory.CreateProducer(_connection, publication).GetAwaiter().GetResult(),
            publication);

        var subscription = new RocketMqSubscription<MyCommand>(
            subscriptionName: new SubscriptionName($"reject-none-{Guid.NewGuid():N}"),
            channelName: new ChannelName(Topic),
            routingKey: routingKey,
            consumerGroup: $"reject-none-{Guid.NewGuid():N}",
            messagePumpType: MessagePumpType.Reactor,
            receiveMessageTimeout: s_receiveMessageTimeout,
            deadLetterRoutingKey: new RoutingKey(DlqTopic));

        _consumer = new RocketMessageConsumerFactory(_connection).Create(subscription);
    }

    public void Dispose()
    {
        _consumer.Dispose();
        _producer.Dispose();
    }

    [Fact]
    public void When_rejecting_a_rocketmq_message_with_no_reason_should_stamp_rejection_reason_none()
    {
        // Arrange
        var message = CreateMessage(new RoutingKey(Topic));
        _producer.Send(message);

        var received = ReceiveOurs(_consumer, message.Id, s_receiveCeiling);
        Assert.NotNull(received);
        var originalTopic = received!.Header.Topic.Value;

        // Act
        _consumer.Reject(received, null);

        // Assert — the message reaches the dead-letter queue stamped rejectionReason="None"
        using var dlqSimpleConsumer = GatewayFactory.CreateSimpleConsumer(_connection, DlqTopic)
            .GetAwaiter().GetResult();
        using var dlqConsumer = new RocketMessageConsumer(dlqSimpleConsumer, 1, TimeSpan.FromSeconds(30));

        var dlqMessage = ReceiveOurs(dlqConsumer, message.Id, s_receiveCeiling);
        Assert.NotNull(dlqMessage);
        dlqConsumer.Acknowledge(dlqMessage!);

        Assert.True(dlqMessage!.Header.Bag.ContainsKey("rejectionReason"));
        Assert.Equal(RejectionReason.None.ToString(), dlqMessage.Header.Bag["rejectionReason"].ToString());

        // verify rejectionMessage is absent (null reason carries no description)
        Assert.False(dlqMessage.Header.Bag.ContainsKey("rejectionMessage"));

        // verify other metadata is present
        Assert.True(dlqMessage.Header.Bag.ContainsKey("originalTopic"));
        Assert.Equal(originalTopic, dlqMessage.Header.Bag["originalTopic"].ToString());
        Assert.True(dlqMessage.Header.Bag.ContainsKey("originalMessageType"));
        Assert.Equal(MessageType.MT_COMMAND.ToString(), dlqMessage.Header.Bag["originalMessageType"].ToString());
        Assert.True(dlqMessage.Header.Bag.ContainsKey("rejectionTimestamp"));

        Assert.Equal(message.Body.Value, dlqMessage.Body.Value);
    }

    private static Message CreateMessage(RoutingKey routingKey) =>
        new(
            new MessageHeader(Id.Random(), routingKey, MessageType.MT_COMMAND,
                contentType: new ContentType(MediaTypeNames.Text.Plain)),
            new MessageBody("reject with no reason"));

    private static Message? ReceiveOurs(IAmAMessageConsumerSync consumer, Id messageId, TimeSpan ceiling)
    {
        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < ceiling)
        {
            foreach (var received in consumer.Receive(s_receiveMessageTimeout))
            {
                if (received.Header.MessageType == MessageType.MT_NONE)
                    continue;
                if (received.Id == messageId)
                    return received;

                consumer.Acknowledge(received);
            }
        }

        return null;
    }
}
