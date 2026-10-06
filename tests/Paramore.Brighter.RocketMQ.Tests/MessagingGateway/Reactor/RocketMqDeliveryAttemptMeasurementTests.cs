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

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Mime;
using System.Threading.Tasks;
using Org.Apache.Rocketmq;
using Paramore.Brighter.MessagingGateway.RocketMQ;
using Paramore.Brighter.RocketMQ.Tests.TestDoubles;
using Paramore.Brighter.RocketMQ.Tests.Utils;
using Xunit;
using Xunit.Abstractions;

namespace Paramore.Brighter.RocketMQ.Tests.MessagingGateway.Reactor;

/// <summary>
/// Task 7.1 (MEASURE) — AC-23, ADR 0077 "Measurement (AC-23)". Not a conformance test: records the
/// raw broker <c>MessageView.DeliveryAttempt</c> over three lease-lapse redeliveries, so R-14's
/// condition can be decided and exactly one of AC-24 / AC-25 claimed. A second fact probes whether
/// <c>SimpleConsumer.ChangeInvisibleDuration</c> works on RocketMQ.Client 5.2.1 (PR #4263 calls it
/// from <c>Nack</c>), because R-14 and AC-25 assume it is blocked upstream.
/// Both facts are committed <c>Skip</c>ped; unskip locally against a clean RocketMQ store to
/// reproduce, then restore the Skip before committing — this measures the broker and the RocketMQ
/// client, not Brighter.
/// </summary>
/// <remarks>
/// Both facts use the pre-created <c>rmq_measure_delivery_attempt</c> topic (the C# client never
/// auto-creates one) and pick their own message out by id, acknowledging anything stale, because a
/// fresh consumer group can be handed messages earlier runs left on the topic.
/// </remarks>
[Trait("Category", "RocketMQ")]
public class RocketMqDeliveryAttemptMeasurementTests(ITestOutputHelper output)
{
    private const string Topic = "rmq_measure_delivery_attempt";
    private const int Deliveries = 3;
    private static readonly TimeSpan s_invisibilityTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan s_receiveMessageTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan s_deliveryCeiling = TimeSpan.FromSeconds(60);

    /// <summary>
    /// AC-23: three deliveries of one message across the 10 s invisibility lapses, with no
    /// <c>ChangeInvisibleDuration</c> call (Brighter's Requeue is a no-op on RocketMQ), reading the raw
    /// <c>MessageView.DeliveryAttempt</c> from <c>Header.Bag["ReceiptHandle"]</c>. The consumer is
    /// disposed and a new one created — same consumer group — between deliveries 2 and 3, so a value
    /// that keeps rising cannot come from the client-local <c>IncrementAndGetDeliveryAttempt</c>.
    /// </summary>
    [Fact(Skip = "measurement — AC-23")]
    public void Measure_delivery_attempt_over_three_lease_lapse_redeliveries_with_a_fresh_client_before_the_third()
    {
        var connection = GatewayFactory.CreateConnection();
        var routingKey = new RoutingKey(Topic);
        var subscription = new RocketMqSubscription<MyCommand>(
            subscriptionName: new SubscriptionName($"measure-{Guid.NewGuid():N}"),
            channelName: new ChannelName(Topic),
            routingKey: routingKey,
            consumerGroup: $"measure-{Guid.NewGuid():N}",
            requeueCount: 3,
            messagePumpType: MessagePumpType.Reactor,
            receiveMessageTimeout: s_receiveMessageTimeout,
            invisibilityTimeout: s_invisibilityTimeout);

        var factory = new RocketMessageConsumerFactory(connection);
        var publication = new RocketMqPublication { Topic = routingKey };
        using var producer = new RocketMqMessageProducer(
            connection,
            GatewayFactory.CreateProducer(connection, publication).GetAwaiter().GetResult(),
            publication);

        var consumer = factory.Create(subscription);
        var observed = new List<int>();

        try
        {
            var message = CreateMessage(routingKey);
            producer.Send(message);
            var sent = Stopwatch.StartNew();

            for (var delivery = 1; delivery <= Deliveries; delivery++)
            {
                if (delivery == Deliveries)
                {
                    // Rule out the client-local counter: the third delivery comes to a new client
                    consumer.Dispose();
                    consumer = factory.Create(subscription);
                    output.WriteLine($"[7.1] consumer disposed and recreated (same group) before delivery {delivery}");
                }

                var received = ReceiveOurs(consumer, message.Id);
                Assert.NotNull(received);

                var view = (MessageView)received!.Header.Bag["ReceiptHandle"];
                observed.Add(view.DeliveryAttempt);
                output.WriteLine(
                    $"[7.1] delivery={delivery} DeliveryAttempt={view.DeliveryAttempt} " +
                    $"HandledCount={received.Header.HandledCount} t+{sent.Elapsed.TotalSeconds:F1}s " +
                    $"brokerMessageId={view.MessageId}");

                // No Requeue/Nack/ChangeInvisibleDuration: the 10 s lease lapses and the broker redelivers
                if (delivery == Deliveries)
                    consumer.Acknowledge(received);
            }

            output.WriteLine($"[7.1] sequence=[{string.Join(", ", observed)}] {Verdict(observed)}");
        }
        finally
        {
            consumer.Dispose();
        }
    }

    /// <summary>
    /// Probe for the R-14 / AC-25 premise: does <c>SimpleConsumer.ChangeInvisibleDuration(view, 0)</c>
    /// on RocketMQ.Client 5.2.1 make the message visible again before its 10 s lease lapses, and what
    /// <c>DeliveryAttempt</c> does the redelivery carry? Records an exception rather than failing on one.
    /// </summary>
    [Fact(Skip = "measurement — AC-23 (ChangeInvisibleDuration probe)")]
    public async Task Probe_change_invisible_duration_to_zero_on_client_5_2_1()
    {
        var connection = GatewayFactory.CreateConnection();
        var consumerGroup = $"probe-{Guid.NewGuid():N}";

        await using var simpleConsumer = await new SimpleConsumer.Builder()
            .SetClientConfig(connection.ClientConfig)
            .SetConsumerGroup(consumerGroup)
            .SetAwaitDuration(s_receiveMessageTimeout)
            .SetSubscriptionExpression(new Dictionary<string, FilterExpression> { [Topic] = new("*") })
            .Build();

        var routingKey = new RoutingKey(Topic);
        var publication = new RocketMqPublication { Topic = routingKey };
        await using var producer = new RocketMqMessageProducer(
            connection,
            await GatewayFactory.CreateProducer(connection, publication),
            publication);

        var message = CreateMessage(routingKey);
        await producer.SendAsync(message);

        var first = await RawReceiveOursAsync(simpleConsumer, message.Id);
        Assert.NotNull(first);
        output.WriteLine($"[7.1-probe] delivery=1 DeliveryAttempt={first!.DeliveryAttempt}");

        var changed = Stopwatch.StartNew();
        try
        {
            await simpleConsumer.ChangeInvisibleDuration(first, TimeSpan.Zero);
            output.WriteLine("[7.1-probe] ChangeInvisibleDuration(view, 0) returned without error");
        }
        catch (Exception e)
        {
            output.WriteLine($"[7.1-probe] ChangeInvisibleDuration(view, 0) threw {e.GetType().FullName}: {e.Message}");
        }

        var second = await RawReceiveOursAsync(simpleConsumer, message.Id);
        Assert.NotNull(second);
        output.WriteLine(
            $"[7.1-probe] delivery=2 DeliveryAttempt={second!.DeliveryAttempt} " +
            $"after {changed.Elapsed.TotalSeconds:F1}s (lease is {s_invisibilityTimeout.TotalSeconds:F0}s; " +
            $"well under it means ChangeInvisibleDuration took effect)");

        await simpleConsumer.Ack(second);
    }

    private static Message CreateMessage(RoutingKey routingKey) =>
        new(
            new MessageHeader(Id.Random(), routingKey, MessageType.MT_COMMAND,
                contentType: new ContentType(MediaTypeNames.Text.Plain)),
            new MessageBody("measure delivery attempt"));

    private Message? ReceiveOurs(IAmAMessageConsumerSync consumer, Id messageId)
    {
        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < s_deliveryCeiling)
        {
            foreach (var received in consumer.Receive(s_receiveMessageTimeout))
            {
                if (received.Header.MessageType == MessageType.MT_NONE)
                    continue;
                if (received.Id == messageId)
                    return received;

                output.WriteLine($"[7.1] acknowledging stale message {received.Id}");
                consumer.Acknowledge(received);
            }
        }

        return null;
    }

    private async Task<MessageView?> RawReceiveOursAsync(SimpleConsumer consumer, Id messageId)
    {
        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < s_deliveryCeiling)
        {
            var views = await consumer.Receive(1, s_invisibilityTimeout);
            foreach (var view in views ?? [])
            {
                if (view.Properties.TryGetValue(HeaderNames.MessageId, out var id) && id == messageId.Value)
                    return view;

                output.WriteLine($"[7.1-probe] acknowledging stale message {view.MessageId}");
                await consumer.Ack(view);
            }
        }

        return null;
    }

    private static string Verdict(IReadOnlyList<int> observed) =>
        observed.Zip(observed.Skip(1)).All(pair => pair.Second > pair.First)
            ? "strictly increasing"
            : "NOT strictly increasing";
}
