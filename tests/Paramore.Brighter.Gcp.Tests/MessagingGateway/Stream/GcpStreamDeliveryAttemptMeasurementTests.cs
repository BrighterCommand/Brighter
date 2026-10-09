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
using Xunit;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Stream;

/// <summary>
/// Task 5.5c (MEASURE) — ADR 0077 Risks, "unverified library behaviours". Not a conformance test:
/// records observed emulator / client-library behaviour so the ADR can be amended with a dated note.
/// Both facts are committed <c>Skip</c>ped; unskip locally against a running emulator to reproduce,
/// then restore the Skip before committing — this measures the Google client library and emulator,
/// not Brighter, so it is not something later code changes should ever need to keep green.
/// </summary>
[Trait("Category", "GcpPubSubStream")]
public class GcpStreamDeliveryAttemptMeasurementTests
{
    private const string DeliveryAttemptAttrKey = "googclient_deliveryattempt";

    /// <summary>
    /// (i) On a DLQ-backed stream subscription, does a message received through
    /// <c>SubscriberClient</c> carry the <c>googclient_deliveryattempt</c> attribute, and does it
    /// parse as the delivery attempt count <see cref="Google.Cloud.PubSub.V1.PubsubExtensions.GetDeliveryAttempt"/>
    /// would read (first delivery, so an attempt of 1 per ADR 0077 assumption A-1)?
    /// Brighter's own <c>Parser</c> copies every attribute not in <c>s_ignoreHeaders</c> into
    /// <c>Header.Bag</c> verbatim, so its presence there is exactly what a real received
    /// <c>PubsubMessage.Attributes</c> would show.
    /// </summary>
    [Fact(Skip = "measurement — ADR 0077 risk (5.5c); see docs/adr/0077-delivery-count-contract.md")]
    public void Measure_whether_a_dlq_backed_stream_subscription_carries_delivery_attempt()
    {
        var provider = new GcpStreamMessageGatewayProvider();
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

        try
        {
            // Arrange
            producer = provider.CreateProducer(publication);
            channel = provider.CreateChannel(subscription);

            var message = builder.SetTopic(routingKey).Build();

            // Act
            producer.Send(message);
            var received = channel.Receive(TimeSpan.FromSeconds(20));
            Assert.NotEqual(MessageType.MT_NONE, received.Header.MessageType);
            channel.Acknowledge(received);

            var hasAttribute = received.Header.Bag.TryGetValue(DeliveryAttemptAttrKey, out var rawValue);
            var attributeValue = rawValue?.ToString();
            var parsesAsInt = int.TryParse(attributeValue, out var parsed);

            // Record — this is a measurement, not a conformance assertion.
            Console.WriteLine(
                $"[5.5c-i] hasAttribute={hasAttribute} value={attributeValue ?? "<absent>"} " +
                $"parsesAsInt={parsesAsInt} parsedValue={(parsesAsInt ? parsed.ToString() : "<n/a>")}");
        }
        finally
        {
            channel?.Dispose();
            provider.CleanUp(producer, channel, []);
        }
    }

    /// <summary>
    /// (ii) Simulates a routed copy carrying a stale <c>googclient_deliveryattempt</c> attribute
    /// forward (ADR 0078 stream routing re-publishes every non-ignored <c>Header.Bag</c> entry,
    /// Parser.cs:350-356). Records whether the emulator accepts a publish carrying that attribute
    /// and, if so, whether the value a receiver sees is the stale one (kept) or a fresh one
    /// (overwritten by <c>SubscriberClient</c>).
    /// </summary>
    [Fact(Skip = "measurement — ADR 0077 risk (5.5c); see docs/adr/0077-delivery-count-contract.md")]
    public void Measure_whether_a_stale_delivery_attempt_attribute_is_accepted_and_overwritten()
    {
        const string staleValue = "999";

        var provider = new GcpStreamMessageGatewayProvider();
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
        var publishAccepted = false;

        try
        {
            // Arrange — a message that already carries a stale delivery-attempt attribute, as a
            // routed copy would if the attribute were not stripped before re-publishing.
            producer = provider.CreateProducer(publication);
            channel = provider.CreateChannel(subscription);

            var message = builder.SetTopic(routingKey).Build();
            message.Header.Bag[DeliveryAttemptAttrKey] = staleValue;

            // Act — Send() throws if the emulator rejects the publish; record whether it does.
            producer.Send(message);
            publishAccepted = true;

            var received = channel.Receive(TimeSpan.FromSeconds(20));
            Assert.NotEqual(MessageType.MT_NONE, received.Header.MessageType);
            channel.Acknowledge(received);

            var hasAttribute = received.Header.Bag.TryGetValue(DeliveryAttemptAttrKey, out var rawValue);
            var attributeValue = rawValue?.ToString();

            // Record — this is a measurement, not a conformance assertion.
            Console.WriteLine(
                $"[5.5c-ii] publishAccepted={publishAccepted} hasAttribute={hasAttribute} " +
                $"value={attributeValue ?? "<absent>"} stalePublished=\"{staleValue}\" " +
                $"overwritten={hasAttribute && attributeValue != staleValue}");
        }
        catch (Exception ex) when (!publishAccepted)
        {
            // Record — the emulator rejected the publish outright.
            Console.WriteLine($"[5.5c-ii] publishAccepted=False rejection={ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            channel?.Dispose();
            provider.CleanUp(producer, channel, []);
        }
    }
}
