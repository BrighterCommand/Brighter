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
/// Task 7.3 — R-5, R-28; ADR 0077 "RocketMQ conditional". A message shaped like a routed
/// dead-letter copy — <see cref="MessageHeader.HandledCount"/> already bumped, but a stale
/// same-named entry left behind in <see cref="MessageHeader.Bag"/> from an earlier receive
/// (<c>RocketMessageConsumer.CreateMessage</c> copies every broker property into the bag
/// verbatim) — must still publish and round-trip with the header's value, not the bag's.
/// </summary>
/// <remarks>
/// Uses the pre-created <c>rmq_stale_hc_r</c> topic (the C# client never auto-creates one) with
/// a fresh consumer group per run, and picks its own message out by id, acknowledging anything
/// stale, because a fresh consumer group can be handed messages earlier runs left on the topic.
/// This test must stay green whether <c>DeliveryCount.Resolve</c> keeps the header count because
/// the <c>rejectionReason</c> discriminator is present (AC-24) or the header property is simply
/// read as today (AC-25) — it is published and read back through this gateway either way.
/// </remarks>
[Trait("Category", "RocketMQ")]
public class RocketMqPublishWithStaleBagHandledCountTests : IDisposable
{
    private const string Topic = "rmq_stale_hc_r";
    private static readonly TimeSpan s_receiveMessageTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan s_receiveCeiling = TimeSpan.FromSeconds(30);

    private readonly RocketMessagingGatewayConnection _connection;
    private readonly RocketMqMessageProducer _producer;
    private readonly IAmAMessageConsumerSync _consumer;

    public RocketMqPublishWithStaleBagHandledCountTests()
    {
        _connection = GatewayFactory.CreateConnection();
        var routingKey = new RoutingKey(Topic);
        var publication = new RocketMqPublication { Topic = routingKey };

        _producer = new RocketMqMessageProducer(
            _connection,
            GatewayFactory.CreateProducer(_connection, publication).GetAwaiter().GetResult(),
            publication);

        var subscription = new RocketMqSubscription<MyCommand>(
            subscriptionName: new SubscriptionName($"stale-hc-{Guid.NewGuid():N}"),
            channelName: new ChannelName(Topic),
            routingKey: routingKey,
            consumerGroup: $"stale-hc-{Guid.NewGuid():N}",
            messagePumpType: MessagePumpType.Reactor,
            receiveMessageTimeout: s_receiveMessageTimeout);

        _consumer = new RocketMessageConsumerFactory(_connection).Create(subscription);
    }

    public void Dispose()
    {
        _consumer.Dispose();
        _producer.Dispose();
    }

    [Fact]
    public void When_publishing_a_rocketmq_message_with_stale_bag_handled_count_should_send_header_value()
    {
        // Arrange - a message shaped like a routed dead-letter copy: the header has already moved
        // on (HandledCount bumped, Subject/SpecVersion/Source changed) but the bag still carries
        // what an earlier receive copied in, and RejectionReason marks it a rejection copy.
        var message = CreateMessage(new RoutingKey(Topic));
        _producer.Send(message);

        // Act
        var received = ReceiveOurs(_consumer, message.Id, s_receiveCeiling);

        // Assert - HandledCount first, so a RED run fails on it specifically
        Assert.NotNull(received);
        Assert.Equal(3, received!.Header.HandledCount);

        // Assert - other header-owned keys behave the same way: the header's value wins, not the
        // stale bag entry
        Assert.Equal("real-subject", received.Header.Subject);
        Assert.Equal("2.5", received.Header.SpecVersion);
        Assert.Equal(new Uri("http://real-source.example"), received.Header.Source);

        _consumer.Acknowledge(received);
    }

    private static Message CreateMessage(RoutingKey routingKey)
    {
        var header = new MessageHeader(
            Id.Random(), routingKey, MessageType.MT_COMMAND,
            contentType: new ContentType(MediaTypeNames.Text.Plain),
            subject: "real-subject",
            handledCount: 3)
        {
            SpecVersion = "2.5",
            Source = new Uri("http://real-source.example")
        };

        // Stale entries left in the bag by an earlier receive (RocketMessageConsumer.CreateMessage
        // copies every broker property into the bag verbatim), now out of step with the header.
        header.Bag[HeaderNames.HandledCount] = "0";
        header.Bag[HeaderNames.Subject] = "stale-subject";
        header.Bag[HeaderNames.SpecVersion] = "9.9";
        header.Bag[HeaderNames.Source] = "http://stale-source.example";

        // Marks this as a Brighter-routed rejection copy (ADR 0077, "Delivery Count Contract").
        header.Bag[RejectionMetadataKeyNames.RejectionReason] = RejectionReason.DeliveryError.ToString();

        return new Message(header, new MessageBody("stale bag handled count"));
    }

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
