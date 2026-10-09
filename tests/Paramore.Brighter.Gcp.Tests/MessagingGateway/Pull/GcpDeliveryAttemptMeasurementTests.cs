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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Api.Gax;
using Google.Cloud.PubSub.V1;
using Paramore.Brighter.Gcp.Tests.Helper;
using Paramore.Brighter.MessagingGateway.GcpPubSub;
using Xunit;
using GcpSubscriptionName = Google.Cloud.PubSub.V1.SubscriptionName;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Pull;

/// <summary>
/// Task 6.7 (MEASURE) — AC-39, ADR 0077 "R-13 (GCP): the branch rule". Not a conformance test:
/// records the raw broker delivery counter the emulator reports over three deliveries on a
/// DLQ-backed subscription (native <c>DeadLetterPolicy</c>, M = 5), so ADR 0077 can be amended with
/// a dated <i>Measurement outcome</i> and the AC-19 / AC-40 branch selected.
/// Both facts are committed <c>Skip</c>ped; unskip locally against a clean emulator to reproduce,
/// then restore the Skip before committing — this measures the emulator and the Google client
/// library, not Brighter.
/// </summary>
/// <remarks>
/// Both facts read the counter with raw Google clients, not through a Brighter channel: Brighter's
/// <c>Parser</c> does not surface <c>ReceivedMessage.DeliveryAttempt</c> yet (that is 6.10/6.11's
/// job), and a Brighter pump requeuing on Stream hangs (#4479). The deferral is the same broker call
/// a deferring handler reaches through Brighter: <c>ModifyAckDeadline(…, 0)</c>, which is what
/// <c>GcpPullMessageConsumer.Requeue</c> sends, and which <c>SubscriberClient.Reply.Nack</c> sends.
/// Every stream delivery is settled (Nack or Ack) so <c>StopAsync</c> cannot block on an
/// outstanding message.
/// </remarks>
[Trait("Category", "GcpPubSubPull")]
public class GcpDeliveryAttemptMeasurementTests
{
    private const int Deliveries = 3;
    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Pull: the raw <c>ReceivedMessage.DeliveryAttempt</c> on each of three deliveries, releasing
    /// the first two with <c>ModifyAckDeadline(…, 0)</c> and acknowledging the third.
    /// </summary>
    [Fact(Skip = "measurement — AC-39")]
    public void Measure_pull_delivery_attempt_over_three_deliveries_on_a_dlq_backed_subscription()
    {
        var provider = new GcpPullMessageGatewayProvider();
        var builder = new DefaultMessageBuilder();

        var routingKey = provider.GetOrCreateRoutingKey();
        var dlqRoutingKey = new RoutingKey($"{routingKey.Value}.DLQ");
        var channelName = provider.GetOrCreateChannelName();

        var publication = provider.CreatePublication(routingKey);
        var subscription = provider.CreateSubscription(
            routingKey, channelName, OnMissingChannel.Create,
            deadLetterRoutingKey: dlqRoutingKey);

        IAmAMessageProducerSync? producer = null;
        IAmAChannelSync? channel = null;
        var observed = new List<int>();

        try
        {
            // Arrange — provision the DLQ-backed subscription through Brighter (5.3), then read raw
            producer = provider.CreateProducer(publication);
            channel = provider.CreateChannel(subscription);

            var client = CreateSubscriberServiceApiClient();
            var subscriptionName = GcpSubscriptionName.FromProjectSubscription(
                GatewayFactory.GetProjectId(), channelName.Value);
            RecordDeadLetterPolicy("pull", client.GetSubscription(subscriptionName));

            producer.Send(builder.SetTopic(routingKey).Build());

            // Act — three deliveries, deferring the first two
            for (var delivery = 1; delivery <= Deliveries; delivery++)
            {
                var received = RawPull(client, subscriptionName);
                Assert.NotNull(received);

                observed.Add(received!.DeliveryAttempt);
                Console.WriteLine(
                    $"[6.7-pull] delivery={delivery} DeliveryAttempt={received.DeliveryAttempt} " +
                    $"messageId={received.Message.MessageId}");

                if (delivery < Deliveries)
                    client.ModifyAckDeadline(subscriptionName, [received.AckId], 0);
                else
                    client.Acknowledge(subscriptionName, [received.AckId]);
            }

            // Record — this is a measurement, not a conformance assertion.
            Console.WriteLine($"[6.7-pull] sequence=[{string.Join(", ", observed)}] {Verdict(observed)}");
        }
        finally
        {
            provider.CleanUp(producer, channel, []);
        }
    }

    /// <summary>
    /// Stream: <c>PubsubExtensions.GetDeliveryAttempt</c> on each of three deliveries through a raw
    /// <c>SubscriberClient</c>, Nacking the first two and Acking the third.
    /// </summary>
    [Fact(Skip = "measurement — AC-39")]
    public async Task Measure_stream_delivery_attempt_over_three_deliveries_on_a_dlq_backed_subscription()
    {
        var provider = new GcpPullMessageGatewayProvider();
        var builder = new DefaultMessageBuilder();

        var routingKey = provider.GetOrCreateRoutingKey();
        var dlqRoutingKey = new RoutingKey($"{routingKey.Value}.DLQ");
        var channelName = provider.GetOrCreateChannelName();

        var publication = provider.CreatePublication(routingKey);
        var subscription = provider.CreateSubscription(
            routingKey, channelName, OnMissingChannel.Create,
            deadLetterRoutingKey: dlqRoutingKey);

        IAmAMessageProducerSync? producer = null;
        IAmAChannelSync? channel = null;
        SubscriberClient? subscriber = null;
        var observed = new ConcurrentQueue<int?>();
        var thirdDelivery = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            // Arrange — provision the DLQ-backed subscription through Brighter (5.3); the
            // subscription mode is client-side, so the same broker subscription serves a stream read
            producer = provider.CreateProducer(publication);
            channel = provider.CreateChannel(subscription);

            var subscriptionName = GcpSubscriptionName.FromProjectSubscription(
                GatewayFactory.GetProjectId(), channelName.Value);
            RecordDeadLetterPolicy("stream", CreateSubscriberServiceApiClient().GetSubscription(subscriptionName));

            subscriber = await new SubscriberClientBuilder
            {
                SubscriptionName = subscriptionName,
                Credential = GatewayFactory.GetCredential(),
                EmulatorDetection = EmulatorDetection.EmulatorOrProduction,
            }.BuildAsync();

            producer.Send(builder.SetTopic(routingKey).Build());

            // Act — three deliveries, Nacking the first two; every delivery is settled
            var run = subscriber.StartAsync((message, _) =>
            {
                observed.Enqueue(message.GetDeliveryAttempt());
                var delivery = observed.Count;
                Console.WriteLine(
                    $"[6.7-stream] delivery={delivery} GetDeliveryAttempt={message.GetDeliveryAttempt()?.ToString() ?? "<null>"} " +
                    $"messageId={message.MessageId}");

                if (delivery < Deliveries)
                    return Task.FromResult(SubscriberClient.Reply.Nack);

                thirdDelivery.TrySetResult(true);
                return Task.FromResult(SubscriberClient.Reply.Ack);
            });

            var completed = await Task.WhenAny(thirdDelivery.Task, Task.Delay(s_timeout));
            Console.WriteLine($"[6.7-stream] thirdDeliveryArrived={completed == thirdDelivery.Task}");

            using var stopTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await subscriber.StopAsync(stopTimeout.Token);
            await run;
            subscriber = null;

            // Record — this is a measurement, not a conformance assertion.
            var values = observed.ToList();
            Console.WriteLine(
                $"[6.7-stream] sequence=[{string.Join(", ", values.Select(v => v?.ToString() ?? "<null>"))}] " +
                $"{(values.All(v => v.HasValue) ? Verdict(values.Select(v => v!.Value).ToList()) : "populated=False")}");
        }
        finally
        {
            if (subscriber != null)
                await subscriber.StopAsync(TimeSpan.FromSeconds(10));
            provider.CleanUp(producer, channel, []);
        }
    }

    private static SubscriberServiceApiClient CreateSubscriberServiceApiClient()
    {
        var connection = new GcpMessagingGatewayConnection
        {
            Credential = GatewayFactory.GetCredential(),
            ProjectId = GatewayFactory.GetProjectId(),
            SubscriptionManagerConfiguration = cfg =>
            {
                cfg.EmulatorDetection = EmulatorDetection.EmulatorOrProduction;
            },
        };
        return connection.GetOrCreateSubscriberServiceApiClient();
    }

    private static ReceivedMessage? RawPull(SubscriberServiceApiClient client, GcpSubscriptionName subscriptionName)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < s_timeout)
        {
            var response = client.Pull(new PullRequest
            {
                SubscriptionAsSubscriptionName = subscriptionName,
                MaxMessages = 1,
            });

            if (response.ReceivedMessages.Count > 0)
                return response.ReceivedMessages[0];

            Thread.Sleep(500);
        }

        return null;
    }

    private static void RecordDeadLetterPolicy(string mode, Google.Cloud.PubSub.V1.Subscription subscription)
    {
        var policy = subscription.DeadLetterPolicy;
        Console.WriteLine(
            $"[6.7-{mode}] deadLetterPolicy={(policy == null ? "<none>" : $"topic={policy.DeadLetterTopic} maxDeliveryAttempts={policy.MaxDeliveryAttempts}")}");
    }

    // A-2 (ADR 0077): populated, strictly increasing, first value 1
    private static string Verdict(IReadOnlyList<int> values)
    {
        var populated = values.All(v => v > 0);
        var strictlyIncreasing = values.Zip(values.Skip(1), (a, b) => b > a).All(x => x);
        var startsAtOne = values.Count > 0 && values[0] == 1;
        return $"populated={populated} strictlyIncreasing={strictlyIncreasing} startsAtOne={startsAtOne} " +
               $"A2Holds={populated && strictlyIncreasing && startsAtOne && values.Count == Deliveries}";
    }
}
