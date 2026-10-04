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
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;
using Xunit;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Pull;

/// <summary>
/// R-17, AC-17 — sync (Reactor) path.
/// A GCP pull Reject with no destination configured (neither <c>deadLetterRoutingKey</c> nor
/// <c>invalidMessageRoutingKey</c>) acknowledges the original message, returns true, and logs a
/// source-generated Warning naming the message id and the rejection reason.
/// </summary>
[Trait("Category", "GcpPubSubPull")]
[Collection("Pull")]
public class GcpPullConsumerRejectNoDestinationWarningTests
{
    /// <summary>
    /// Neither a DLQ nor an invalid-message key is configured: the original is acknowledged
    /// (a second Receive returns MT_NONE), Reject returns true, and one Warning log event names
    /// the message id and contains "Unacceptable".
    /// </summary>
    [Fact]
    public void When_rejecting_with_no_destination_on_pull_should_ack_and_log_warning()
    {
        var provider = new GcpPullMessageGatewayProvider();
        var builder = new DefaultMessageBuilder();

        var routingKey = provider.GetOrCreateRoutingKey();
        var channelName = provider.GetOrCreateChannelName();

        var publication = provider.CreatePublication(routingKey);
        var subscription = provider.CreateSubscription(
            routingKey, channelName, OnMissingChannel.Create);

        IAmAMessageProducerSync? producer = null;
        IAmAChannelSync? channel = null;

        try
        {
            // Arrange
            producer = provider.CreateProducer(publication);
            channel = provider.CreateChannel(subscription);

            var message = builder.SetTopic(routingKey).Build();
            producer.Send(message);

            var received = channel.Receive(TimeSpan.FromSeconds(10));
            Assert.NotEqual(MessageType.MT_NONE, received.Header.MessageType);

            using var logContext = TestCorrelator.CreateContext();

            // Act
            var result = channel.Reject(received, new MessageRejectionReason(RejectionReason.Unacceptable, "bad payload"));

            // Assert — Reject returns true
            Assert.True(result);

            // Assert — source is acked: a second Receive returns MT_NONE
            var reRead = channel.Receive(TimeSpan.FromSeconds(5));
            Assert.Equal(MessageType.MT_NONE, reRead.Header.MessageType);

            // Assert — a Warning names the message id and the reason
            var warning = Assert.Single(TestCorrelator.GetLogEventsFromCurrentContext(),
                e => e.Level == LogEventLevel.Warning && e.RenderMessage().Contains(received.Id.Value));
            Assert.Contains("Unacceptable", warning.RenderMessage());
        }
        finally
        {
            provider.CleanUp(producer, channel, []);
        }
    }
}
