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
using System.Threading;
using Xunit;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Stream;

/// <summary>
/// R-19, AC-18 — sync (Reactor) path.
/// A failed GCP routing publish releases (Nacks) the original message for prompt redelivery
/// instead of accepting it, returns true, and leaves the destination (dead-letter) topic
/// uncreated. Covers both the publish-call failure (destination producer built with
/// <see cref="OnMissingChannel.Assume"/> against a topic that never existed) and the producer
/// construction failure (destination built with <see cref="OnMissingChannel.Validate"/>, ADR 0078
/// "Divergence from SQS"). Every Receive is settled (Reject or Acknowledge) before the channel is
/// disposed, so Dispose cannot hang (see this spec's GCP Stream ack-before-dispose gotcha).
/// </summary>
[Trait("Category", "GcpPubSubStream")]
[Collection("Stream")]
public class GcpStreamConsumerRejectRoutingFailureReleasesOriginalTests
{
    private const int WindowSeconds = 10;

    /// <summary>
    /// Assume-configured destination: the publish call itself fails because the destination topic
    /// does not exist. Reject Nacks the original (proven by redelivery within W), returns true, and
    /// the destination topic is still absent. The Reject call itself is bounded by W (ADR 0078 Risk:
    /// GcpMessageProducer.Dispose() is sync-over-async and could theoretically block).
    /// </summary>
    [Fact]
    public void When_rejection_routing_publish_fails_on_stream_with_assume_should_release_original_for_redelivery()
    {
        var provider = new GcpStreamMessageGatewayProvider();
        var builder = new DefaultMessageBuilder();

        var routingKey = provider.GetOrCreateRoutingKey();
        var channelName = provider.GetOrCreateChannelName();
        var deadLetterRoutingKey = new RoutingKey($"{routingKey.Value}.DLQ");

        var (provisioning, underTest) = provider.CreateFailedRoutingGiven(
            routingKey, channelName, deadLetterRoutingKey, OnMissingChannel.Assume);

        IAmAMessageProducerSync? producer = null;
        IAmAChannelSync? channel = null;

        try
        {
            // Arrange — stand up the source topic/subscription (Pull-mode provisioning, ADR 0078),
            // then attach the under-test (Stream-mode) channel.
            provider.CreateChannel(provisioning).Dispose();

            producer = provider.CreateProducer(provider.CreatePublication(routingKey));
            channel = provider.CreateChannel(underTest);

            var message = builder.SetTopic(routingKey).Build();
            producer.Send(message);

            var received = channel.Receive(TimeSpan.FromSeconds(20));
            Assert.NotEqual(MessageType.MT_NONE, received.Header.MessageType);

            // Act — the routing publish fails (destination topic never existed); Reject itself must
            // stay within W (the sync failure path disposes the producer sync-over-async).
            var stopwatch = Stopwatch.StartNew();
            var result = channel.Reject(received, new MessageRejectionReason(RejectionReason.DeliveryError, "publish failure"));
            stopwatch.Stop();

            // Assert — Reject returns true and completed within W
            Assert.True(result);
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(WindowSeconds),
                $"Reject took {stopwatch.Elapsed} which exceeds the {WindowSeconds}s risk window");

            // Assert — the original is Nacked (not accepted): it is redelivered within W
            Message redelivered = new Message();
            var pollStopwatch = Stopwatch.StartNew();
            while (pollStopwatch.Elapsed < TimeSpan.FromSeconds(WindowSeconds))
            {
                redelivered = channel.Receive(TimeSpan.FromSeconds(1));
                if (redelivered.Header.MessageType != MessageType.MT_NONE)
                    break;
                Thread.Sleep(250);
            }

            Assert.NotEqual(MessageType.MT_NONE, redelivered.Header.MessageType);
            Assert.Equal(received.Id, redelivered.Id);

            // Settle the redelivered message — mandatory: an un-acked Stream receive hangs Dispose.
            channel.Acknowledge(redelivered);

            // Assert — the destination topic was never created
            Assert.False(provider.TopicExists(deadLetterRoutingKey));
        }
        finally
        {
            channel?.Dispose();
            provider.CleanUp(producer, channel, []);
        }
    }

    /// <summary>
    /// Validate-configured destination (ADR 0078 "Divergence from SQS"): the destination producer's
    /// construction itself throws (the topic doesn't exist and Validate checks before publishing),
    /// which GcpRejectionRouter catches the same way. Reject still Nacks the original (proven by
    /// redelivery within W), returns true, and the destination topic is still absent. Exactly one
    /// Reject is issued in this test (the topic-existence check cache trap — see ADR 0078 /
    /// tasks.md 5.8).
    /// </summary>
    [Fact]
    public void When_rejection_routing_publish_fails_on_stream_with_validate_should_release_original_for_redelivery()
    {
        var provider = new GcpStreamMessageGatewayProvider();
        var builder = new DefaultMessageBuilder();

        var routingKey = provider.GetOrCreateRoutingKey();
        var channelName = provider.GetOrCreateChannelName();
        var deadLetterRoutingKey = new RoutingKey($"{routingKey.Value}.Invalid2.DLQ");

        var (provisioning, underTest) = provider.CreateFailedRoutingGiven(
            routingKey, channelName, deadLetterRoutingKey, OnMissingChannel.Validate);

        IAmAMessageProducerSync? producer = null;
        IAmAChannelSync? channel = null;

        try
        {
            // Arrange — stand up the source topic/subscription (Pull-mode provisioning, ADR 0078),
            // then attach the under-test (Stream-mode) channel.
            provider.CreateChannel(provisioning).Dispose();

            producer = provider.CreateProducer(provider.CreatePublication(routingKey));
            channel = provider.CreateChannel(underTest);

            var message = builder.SetTopic(routingKey).Build();
            producer.Send(message);

            var received = channel.Receive(TimeSpan.FromSeconds(20));
            Assert.NotEqual(MessageType.MT_NONE, received.Header.MessageType);

            // Act — the destination producer construction fails Validate; exactly one Reject call.
            var stopwatch = Stopwatch.StartNew();
            var result = channel.Reject(received, new MessageRejectionReason(RejectionReason.DeliveryError, "validate failure"));
            stopwatch.Stop();

            // Assert — Reject returns true and completed within W
            Assert.True(result);
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(WindowSeconds),
                $"Reject took {stopwatch.Elapsed} which exceeds the {WindowSeconds}s risk window");

            // Assert — the original is Nacked (not accepted): it is redelivered within W
            Message redelivered = new Message();
            var pollStopwatch = Stopwatch.StartNew();
            while (pollStopwatch.Elapsed < TimeSpan.FromSeconds(WindowSeconds))
            {
                redelivered = channel.Receive(TimeSpan.FromSeconds(1));
                if (redelivered.Header.MessageType != MessageType.MT_NONE)
                    break;
                Thread.Sleep(250);
            }

            Assert.NotEqual(MessageType.MT_NONE, redelivered.Header.MessageType);
            Assert.Equal(received.Id, redelivered.Id);

            // Settle the redelivered message — mandatory: an un-acked Stream receive hangs Dispose.
            channel.Acknowledge(redelivered);

            // Assert — the destination topic was never created
            Assert.False(provider.TopicExists(deadLetterRoutingKey));
        }
        finally
        {
            channel?.Dispose();
            provider.CleanUp(producer, channel, []);
        }
    }
}
