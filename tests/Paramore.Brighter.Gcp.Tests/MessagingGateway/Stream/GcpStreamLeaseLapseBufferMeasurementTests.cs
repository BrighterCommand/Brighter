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
using Google.Api.Gax;
using Google.Cloud.PubSub.V1;
using Paramore.Brighter.Gcp.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.GcpPubSub;
using Xunit;
using DeadLetterPolicy = Paramore.Brighter.MessagingGateway.GcpPubSub.DeadLetterPolicy;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Stream;

/// <summary>
/// Task 6.13 (MEASURE) — ADR 0077 Risks and the "GCP stream consumer lease-lapse procedure". Not a
/// conformance test: records whether, with <c>bufferSize: 2</c>, <c>noOfPerformers: 1</c> and
/// <c>MaxTotalAckExtension = 10 s</c>, the stream client admits the lease-lapse redelivery of a
/// message while its first delivery is still held (unsettled). The outcome decides how 6.14 is run,
/// and whether <c>StreamOrdering</c> needs the keyless alternative.
/// Committed <c>Skip</c>ped; unskip locally against a running emulator to reproduce, then restore the
/// Skip before committing. It measures the Google client library and the emulator, not Brighter.
/// </summary>
[Trait("Category", "GcpPubSubStream")]
public class GcpStreamLeaseLapseBufferMeasurementTests
{
    /// <summary>
    /// Stream (no ordering): hold m1, poll for its lease-lapse redelivery. Run with the client's own
    /// <c>AckDeadline</c> left at its default, and set to 10 s to match the subscription.
    /// </summary>
    [Theory(Skip = "measurement — ADR 0077 stream lease-lapse procedure (6.13); see docs/adr/0077-delivery-count-contract.md")]
    [InlineData(false)]
    [InlineData(true)]
    public void Measure_whether_buffer_size_two_admits_a_stream_redelivery_while_the_first_is_held(
        bool clientAckDeadlineTenSeconds)
    {
        var provider = new GcpStreamMessageGatewayProvider();
        var routingKey = provider.GetOrCreateRoutingKey();
        var channelName = provider.GetOrCreateChannelName();

        IAmAMessageProducerSync? producer = null;
        IAmAChannelSync? channel = null;
        try
        {
            producer = provider.CreateProducer(provider.CreatePublication(routingKey));
            channel = provider.CreateChannel(
                LeaseLapseMeasurement.Subscription(routingKey, channelName, enableMessageOrdering: false,
                    clientAckDeadlineTenSeconds));

            LeaseLapseMeasurement.Run($"6.13 Stream clientAckDeadline10s={clientAckDeadlineTenSeconds}",
                producer, channel, routingKey, partitionKey: null);
        }
        finally
        {
            provider.CleanUp(producer, channel, []);
        }
    }
}

/// <summary>
/// Task 6.13 (MEASURE) on an ordering-enabled subscription, with and without an ordering key. See
/// <see cref="GcpStreamLeaseLapseBufferMeasurementTests"/>.
/// </summary>
[Trait("Category", "GcpPubSubStreamOrdering")]
public class GcpStreamOrderingLeaseLapseBufferMeasurementTests
{
    /// <summary>StreamOrdering, message published with an ordering key.</summary>
    [Theory(Skip = "measurement — ADR 0077 stream lease-lapse procedure (6.13); see docs/adr/0077-delivery-count-contract.md")]
    [InlineData(false)]
    [InlineData(true)]
    public void Measure_whether_buffer_size_two_admits_a_keyed_stream_ordering_redelivery_while_the_first_is_held(
        bool clientAckDeadlineTenSeconds)
        => Measure("6.13 StreamOrdering keyed", new PartitionKey("lease-lapse-key"), clientAckDeadlineTenSeconds);

    /// <summary>StreamOrdering, message published without an ordering key (the keyless alternative).</summary>
    [Theory(Skip = "measurement — ADR 0077 stream lease-lapse procedure (6.13); see docs/adr/0077-delivery-count-contract.md")]
    [InlineData(false)]
    [InlineData(true)]
    public void Measure_whether_buffer_size_two_admits_a_keyless_stream_ordering_redelivery_while_the_first_is_held(
        bool clientAckDeadlineTenSeconds)
        => Measure("6.13 StreamOrdering keyless", partitionKey: null, clientAckDeadlineTenSeconds);

    private static void Measure(string label, PartitionKey? partitionKey, bool clientAckDeadlineTenSeconds)
    {
        var provider = new GcpStreamOrderingMessageGatewayProvider();
        var routingKey = provider.GetOrCreateRoutingKey();
        var channelName = provider.GetOrCreateChannelName();

        IAmAMessageProducerSync? producer = null;
        IAmAChannelSync? channel = null;
        try
        {
            producer = provider.CreateProducer(provider.CreatePublication(routingKey));
            channel = provider.CreateChannel(
                LeaseLapseMeasurement.Subscription(routingKey, channelName, enableMessageOrdering: true,
                    clientAckDeadlineTenSeconds));

            LeaseLapseMeasurement.Run($"{label} clientAckDeadline10s={clientAckDeadlineTenSeconds}",
                producer, channel, routingKey, partitionKey);
        }
        finally
        {
            provider.CleanUp(producer, channel, []);
        }
    }
}

/// <summary>The shared Given and the hold-and-poll procedure for the 6.13 measurement.</summary>
file static class LeaseLapseMeasurement
{
    private const int DECISION_WINDOW_SECONDS = 45;
    private const int OBSERVATION_WINDOW_SECONDS = 90;
    private static readonly TimeSpan s_pollInterval = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// The 6.13 / 6.14 configuration: <c>bufferSize: 2</c>, <c>noOfPerformers: 1</c>,
    /// <c>AckDeadlineSeconds = 10</c>, a native <c>DeadLetterPolicy</c> (M = 5) so the delivery
    /// attempt is populated, and a <c>StreamingConfiguration</c> capping lease extension at 10 s.
    /// With <paramref name="clientAckDeadlineTenSeconds"/>, the client's own <c>AckDeadline</c> (the
    /// stream ack deadline it asks the server for, default 60 s) is also set to 10 s.
    /// </summary>
    public static GcpPubSubSubscription Subscription(
        RoutingKey routingKey, ChannelName channelName, bool enableMessageOrdering,
        bool clientAckDeadlineTenSeconds) =>
        new(
            subscriptionName: new SubscriptionName(channelName),
            channelName: channelName,
            routingKey: routingKey,
            requestType: typeof(MyCommand),
            bufferSize: 2,
            noOfPerformers: 1,
            messagePumpType: MessagePumpType.Reactor,
            ackDeadlineSeconds: 10,
            enableMessageOrdering: enableMessageOrdering,
            deadLetter: new DeadLetterPolicy(
                new RoutingKey($"{routingKey.Value}.native"),
                new ChannelName($"{routingKey.Value}.native"))
            {
                AckDeadlineSeconds = 60,
                MaxDeliveryAttempts = 5,
                PublisherMember = GcpEmulatorIamMember.Value,
            },
            makeChannels: OnMissingChannel.Create,
            subscriptionMode: SubscriptionMode.Stream,
            // The connection's StreamConfiguration runs before this one (#4516), so repeating its
            // emulator detection here is redundant but harmless.
            streamingConfiguration: builder =>
            {
                builder.EmulatorDetection = EmulatorDetection.EmulatorOrProduction;
                builder.Settings = new SubscriberClient.Settings { MaxTotalAckExtension = TimeSpan.FromSeconds(10) };
                if (clientAckDeadlineTenSeconds) builder.Settings.AckDeadline = TimeSpan.FromSeconds(10);
            },
            subscriberMember: GcpEmulatorIamMember.Value);

    /// <summary>
    /// Publishes one message, receives and holds m1 unsettled, then polls <c>Receive</c> every
    /// 500 ms. Records each arrival's time since m1 and its <c>HandledCount</c>; the decision is
    /// whether m1's redelivery arrives within 45 s. Polling continues to 90 s so a late redelivery is
    /// still recorded. Every received message is acknowledged at the end.
    /// </summary>
    public static void Run(
        string label, IAmAMessageProducerSync producer, IAmAChannelSync channel, RoutingKey routingKey,
        PartitionKey? partitionKey)
    {
        var builder = new DefaultMessageBuilder().SetTopic(routingKey);
        if (partitionKey is not null) builder.SetPartitionKey(partitionKey);
        var message = builder.Build();
        producer.Send(message);

        var m1 = channel.Receive(TimeSpan.FromSeconds(20));
        Assert.NotEqual(MessageType.MT_NONE, m1.Header.MessageType);
        var held = Stopwatch.StartNew();
        Console.WriteLine($"[{label}] m1 id={m1.Id} count={m1.Header.HandledCount} " +
                          $"orderingKey={(partitionKey is null ? "<none>" : partitionKey.Value)}");

        var received = new List<Message> { m1 };
        TimeSpan? redeliveredAt = null;
        while (held.Elapsed < TimeSpan.FromSeconds(OBSERVATION_WINDOW_SECONDS))
        {
            var next = channel.Receive(s_pollInterval);
            if (next.Header.MessageType == MessageType.MT_NONE) continue;

            received.Add(next);
            Console.WriteLine($"[{label}] +{held.Elapsed.TotalSeconds:F1}s id={next.Id} " +
                              $"count={next.Header.HandledCount} sameAsM1={next.Id == m1.Id}");
            if (next.Id == m1.Id && redeliveredAt is null)
            {
                redeliveredAt = held.Elapsed;
                break;
            }
        }

        var withinDecisionWindow = redeliveredAt is { } at && at <= TimeSpan.FromSeconds(DECISION_WINDOW_SECONDS);
        Console.WriteLine($"[{label}] RESULT redelivered={redeliveredAt is not null} " +
                          $"at={(redeliveredAt is { } t ? $"{t.TotalSeconds:F1}s" : "<none in 90s>")} " +
                          $"within45s={withinDecisionWindow}");

        foreach (var m in received) channel.Acknowledge(m);
    }
}
