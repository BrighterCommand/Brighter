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
using System.Threading.Tasks;
using Xunit;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Pull;

/// <summary>
/// R-19, AC-18 — async (Proactor) path.
/// A failed GCP routing publish releases the original message for prompt redelivery instead of
/// acknowledging it, returns true, and leaves the destination (dead-letter) topic uncreated.
/// Covers both the publish-call failure (destination producer built with
/// <see cref="OnMissingChannel.Assume"/> against a topic that never existed) and the producer
/// construction failure (destination built with <see cref="OnMissingChannel.Validate"/>, ADR 0078
/// "Divergence from SQS"). The async path disposes the failed producer via DisposeAsync() properly,
/// so this file does not need the sync file's Stopwatch risk assertion on RejectAsync itself.
/// </summary>
[Trait("Category", "GcpPubSubPull")]
[Collection("Pull")]
public class GcpPullConsumerRejectRoutingFailureReleasesOriginalAsyncTests
{
    private const int WindowSeconds = 10;

    /// <summary>
    /// Assume-configured destination: the publish call itself fails because the destination topic
    /// does not exist. RejectAsync releases the original (proven by redelivery within W), returns
    /// true, and the destination topic is still absent.
    /// </summary>
    [Fact]
    public async Task When_rejection_routing_publish_fails_async_on_pull_with_assume_should_release_original_for_redelivery()
    {
        var provider = new GcpPullMessageGatewayProvider();
        var builder = new DefaultMessageBuilder();

        var routingKey = provider.GetOrCreateRoutingKey();
        var channelName = provider.GetOrCreateChannelName();
        var deadLetterRoutingKey = new RoutingKey($"{routingKey.Value}.DLQ");

        var (provisioning, underTest) = provider.CreateFailedRoutingGiven(
            routingKey, channelName, deadLetterRoutingKey, OnMissingChannel.Assume);

        IAmAMessageProducerAsync? producer = null;
        IAmAChannelAsync? channel = null;

        try
        {
            // Arrange — stand up the source topic/subscription, then attach the under-test channel.
            (await provider.CreateChannelAsync(provisioning)).Dispose();

            producer = await provider.CreateProducerAsync(provider.CreatePublication(routingKey));
            channel = await provider.CreateChannelAsync(underTest);

            var message = builder.SetTopic(routingKey).Build();
            await producer.SendAsync(message);

            var received = await channel.ReceiveAsync(TimeSpan.FromSeconds(10));
            Assert.NotEqual(MessageType.MT_NONE, received.Header.MessageType);

            // Act — the routing publish fails (destination topic never existed).
            var result = await channel.RejectAsync(received, new MessageRejectionReason(RejectionReason.DeliveryError, "publish failure"));

            // Assert — RejectAsync returns true
            Assert.True(result);

            // Assert — the original is released (not acked): it is redelivered within W
            Message redelivered = new Message();
            var pollStopwatch = Stopwatch.StartNew();
            while (pollStopwatch.Elapsed < TimeSpan.FromSeconds(WindowSeconds))
            {
                redelivered = await channel.ReceiveAsync(TimeSpan.FromSeconds(1));
                if (redelivered.Header.MessageType != MessageType.MT_NONE)
                    break;
                await Task.Delay(250);
            }

            Assert.NotEqual(MessageType.MT_NONE, redelivered.Header.MessageType);
            Assert.Equal(received.Id, redelivered.Id);

            // Settle the redelivered message so it does not keep redeliverying into later tests.
            await channel.AcknowledgeAsync(redelivered);

            // Assert — the destination topic was never created
            Assert.False(await provider.TopicExistsAsync(deadLetterRoutingKey));
        }
        finally
        {
            await provider.CleanUpAsync(producer, channel, []);
        }
    }

    /// <summary>
    /// Validate-configured destination (ADR 0078 "Divergence from SQS"): the destination producer's
    /// construction itself throws, which GcpRejectionRouter catches the same way. RejectAsync still
    /// releases the original (proven by redelivery within W), returns true, and the destination
    /// topic is still absent. Exactly one RejectAsync is issued in this test (the topic-existence
    /// check cache trap — see ADR 0078 / tasks.md 5.8).
    /// </summary>
    [Fact]
    public async Task When_rejection_routing_publish_fails_async_on_pull_with_validate_should_release_original_for_redelivery()
    {
        var provider = new GcpPullMessageGatewayProvider();
        var builder = new DefaultMessageBuilder();

        var routingKey = provider.GetOrCreateRoutingKey();
        var channelName = provider.GetOrCreateChannelName();
        var deadLetterRoutingKey = new RoutingKey($"{routingKey.Value}.Invalid2.DLQ");

        var (provisioning, underTest) = provider.CreateFailedRoutingGiven(
            routingKey, channelName, deadLetterRoutingKey, OnMissingChannel.Validate);

        IAmAMessageProducerAsync? producer = null;
        IAmAChannelAsync? channel = null;

        try
        {
            // Arrange — stand up the source topic/subscription, then attach the under-test channel.
            (await provider.CreateChannelAsync(provisioning)).Dispose();

            producer = await provider.CreateProducerAsync(provider.CreatePublication(routingKey));
            channel = await provider.CreateChannelAsync(underTest);

            var message = builder.SetTopic(routingKey).Build();
            await producer.SendAsync(message);

            var received = await channel.ReceiveAsync(TimeSpan.FromSeconds(10));
            Assert.NotEqual(MessageType.MT_NONE, received.Header.MessageType);

            // Act — the destination producer construction fails Validate; exactly one RejectAsync call.
            var result = await channel.RejectAsync(received, new MessageRejectionReason(RejectionReason.DeliveryError, "validate failure"));

            // Assert — RejectAsync returns true
            Assert.True(result);

            // Assert — the original is released (not acked): it is redelivered within W
            Message redelivered = new Message();
            var pollStopwatch = Stopwatch.StartNew();
            while (pollStopwatch.Elapsed < TimeSpan.FromSeconds(WindowSeconds))
            {
                redelivered = await channel.ReceiveAsync(TimeSpan.FromSeconds(1));
                if (redelivered.Header.MessageType != MessageType.MT_NONE)
                    break;
                await Task.Delay(250);
            }

            Assert.NotEqual(MessageType.MT_NONE, redelivered.Header.MessageType);
            Assert.Equal(received.Id, redelivered.Id);

            // Settle the redelivered message so it does not keep redeliverying into later tests.
            await channel.AcknowledgeAsync(redelivered);

            // Assert — the destination topic was never created
            Assert.False(await provider.TopicExistsAsync(deadLetterRoutingKey));
        }
        finally
        {
            await provider.CleanUpAsync(producer, channel, []);
        }
    }
}
