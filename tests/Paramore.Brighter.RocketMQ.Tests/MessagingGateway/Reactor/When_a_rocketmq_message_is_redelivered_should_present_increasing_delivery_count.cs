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
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.RocketMQ;
using Paramore.Brighter.RocketMQ.Tests.Utils;
using Xunit;

namespace Paramore.Brighter.RocketMQ.Tests.MessagingGateway.Reactor;

/// <summary>
/// Task 7.10 TEST — R-1, R-2, R-3, AC-1, AC-2. Task 7.1's measurement recorded RocketMQ's
/// <c>MessageView.DeliveryAttempt</c> as <c>1, 2, 3</c> across lease-lapse redeliveries, but
/// classified it APPROXIMATE (not EXACT), so this test only asserts that the first delivery's
/// <c>Header.HandledCount</c> is <c>0</c> and that the sequence is strictly increasing — not exact
/// values. This test drives the production pump (<see cref="ConformanceDeferredPump"/>), not a
/// harness stand-in for the rule.
/// </summary>
/// <remarks>
/// <para>
/// When this test was written, Brighter's RocketMQ <c>Requeue</c> was a broker no-op and redelivery
/// came only when the subscription's invisibility lease lapsed; the timings described here assume that.
/// Since bugfix 0025 <c>Requeue</c> sets the invisible duration on the broker, so redelivery comes
/// sooner. The assertions do not depend on the lease timing.
/// <c>requeueCount: -1</c>
/// disables Brighter's own delivery budget (see <c>RocketMqBudgetMinusOneNeverRejectsReactorTests</c>,
/// task 7.5), so no DLQ is needed here: the pump simply keeps requeuing forever and the test quits it
/// once the handler has dispatched three times.
/// </para>
/// <para>
/// Uses the pre-created <c>rmq_delivery_count_r</c> topic (the C# client never auto-creates one) with
/// a fresh consumer group per run. The consumer is built directly via
/// <see cref="RocketMessageConsumerFactory"/> and wrapped with
/// <see cref="ConformanceDeferredPump.CreateRecordingChannel"/> so each delivery's
/// <c>Header.HandledCount</c> is recorded before the pump mutates it — the HandledCount log is keyed
/// per message id, so a stale message left by an earlier run on a fresh consumer group would simply
/// record under a different key and not pollute this run's sequence.
/// </para>
/// </remarks>
[Trait("Category", "RocketMQ")]
// Shares ConformanceDeferredPump's static dispatch/requeue/handled-count state with the generated
// pump tests, so it runs in their collection: a ResetDispatchCount from a parallel test would wipe
// this test's recorded state.
[Collection("RocketMQMessagingGateway")]
public class RocketMqRedeliveryDeliveryCountReactorTests
{
    private const string Topic = "rmq_delivery_count_r";
    private static readonly TimeSpan s_invisibilityTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan s_receiveMessageTimeout = TimeSpan.FromSeconds(2);

    [Fact]
    public async Task When_a_rocketmq_message_is_redelivered_should_present_increasing_delivery_count()
    {
        // Arrange
        ConformanceDeferredPump.ResetDispatchCount();

        var provider = new RocketMqMessageGatewayProvider();
        var routingKey = new RoutingKey(Topic);
        var channelName = provider.GetOrCreateChannelName();

        var publication = provider.CreatePublication(routingKey);

        // Subscription: budget = -1 (Brighter's own budget disabled, task 7.5), 10 s invisibility
        // lease. A fresh consumer group avoids picking up
        // any message an earlier run left on the topic.
        var subscription = new RocketMqSubscription<ConformanceDeferredCommand>(
            subscriptionName: new SubscriptionName($"delivery-count-{Guid.NewGuid():N}"),
            channelName: channelName,
            routingKey: routingKey,
            consumerGroup: $"delivery-count-{Guid.NewGuid():N}",
            messagePumpType: MessagePumpType.Reactor,
            requeueCount: -1,
            receiveMessageTimeout: s_receiveMessageTimeout,
            invisibilityTimeout: s_invisibilityTimeout);

        IAmAMessageProducerSync? producer = null;
        IAmAChannelSync? channel = null;

        try
        {
            producer = provider.CreateProducer(publication);

            // Build the consumer directly (not provider.CreateChannel) so it can be wrapped with
            // the recording decorator that logs Header.HandledCount on each Receive.
            var consumerFactory = new RocketMessageConsumerFactory(GatewayFactory.CreateConnection());
            var consumer = consumerFactory.Create(subscription);
            channel = ConformanceDeferredPump.CreateRecordingChannel(channelName, routingKey, consumer);

            var cmd = new ConformanceDeferredCommand { Value = "rocketmq redelivery delivery count test" };
            var message = new Message(
                new MessageHeader(cmd.Id, routingKey, MessageType.MT_COMMAND),
                new MessageBody(JsonSerializer.Serialize(cmd, JsonSerialisationOptions.Options)));

            producer.Send(message);

            // Act — run the production pump with no Brighter-side budget (requeueCount: -1); the
            // RocketMQ Requeue sets the invisible duration to zero, so the broker redelivers within
            // seconds. Quit once the handler has been invoked 3 times.
            var pump = ConformanceDeferredPump.CreateReactor(channel, -1, TimeSpan.FromMilliseconds(5000));
            var pumping = Task.Factory.StartNew(() => pump.Run(), TaskCreationOptions.LongRunning);

            var key = ConformanceDeferredPump.KeyOf(message);
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(60)
                   && ConformanceDeferredPump.GetDispatchCount(key) < 3)
            {
                await Task.Delay(100);
            }

            channel.Enqueue(MessageFactory.CreateQuitMessage(routingKey));
            await pumping;

            // Assert — read the HandledCount sequence after pump quit-and-await
            var counts = ConformanceDeferredPump.GetHandledCountLog(key);
            Assert.True(counts.Count >= 3,
                $"expected at least 3 deliveries but recorded {counts.Count}");

            // R-2 / AC-2: the first delivery presents 0
            Assert.Equal(0, counts[0]);

            // R-1 / AC-1 / R-3: every subsequent delivery presents a strictly greater count
            for (var i = 1; i < counts.Count; i++)
            {
                Assert.True(counts[i] > counts[i - 1],
                    $"R-1: delivery {i + 1} count {counts[i]} must be strictly greater than "
                    + $"delivery {i} count {counts[i - 1]}");
            }
        }
        finally
        {
            provider.CleanUp(producer, channel, []);
        }
    }
}
