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
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.GcpPubSub;
using Xunit;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Stream;

/// <summary>
/// #4479 — a pump that requeues on a GCP Stream channel hangs when it quits. After a requeue the
/// <c>SubscriberClient</c> pushes the redelivery into the consumer's buffer and parks its callback
/// until Brighter settles it; a pump that reads its quit first never reads that message, and
/// disposing the channel must not then wait on it (the shutdown waited ~59.5 min for it).
/// </summary>
[Trait("Category", "GcpPubSubStream")]
[Collection("Stream")]
public class GcpStreamDisposeWithBufferedRedeliveryTests
{
    private static readonly TimeSpan s_disposeBound = TimeSpan.FromSeconds(10);

    [Fact]
    public void When_a_gcp_stream_channel_is_disposed_with_a_redelivery_buffered_after_requeue_should_return_promptly()
    {
        var provider = new GcpStreamMessageGatewayProvider();
        var builder = new DefaultMessageBuilder();

        var routingKey = provider.GetOrCreateRoutingKey();
        var channelName = provider.GetOrCreateChannelName();

        var publication = provider.CreatePublication(routingKey);
        var subscription = provider.CreateSubscription(routingKey, channelName, OnMissingChannel.Create);

        IAmAMessageProducerSync? producer = null;
        IAmAChannelSync? channel = null;
        IAmAChannelSync? redeliveryChannel = null;
        var disposeAttempted = false;

        try
        {
            // Arrange — requeue the first delivery, then give the client time to buffer the
            // redelivery unread, as it is when a pump dequeues its quit message first
            producer = provider.CreateProducer(publication);
            channel = provider.CreateChannel(subscription);

            producer.Send(builder.SetTopic(routingKey).Build());
            var first = channel.Receive(TimeSpan.FromSeconds(20));
            Assert.NotEqual(MessageType.MT_NONE, first.Header.MessageType);

            channel.Requeue(first);
            Thread.Sleep(TimeSpan.FromSeconds(2));

            // Act
            disposeAttempted = true;
            var disposed = Task.Run(channel.Dispose).Wait(s_disposeBound);

            // Assert — dispose returns promptly, and the buffered message is still redeliverable
            Assert.True(disposed, $"Disposing the stream channel did not return within {s_disposeBound}");

            // a fresh subscription instance, so the factory builds a new stream consumer
            redeliveryChannel = provider.CreateChannel(
                provider.CreateSubscription(routingKey, channelName, OnMissingChannel.Assume));
            var redelivered = redeliveryChannel.Receive(TimeSpan.FromSeconds(20));
            Assert.Equal(first.Id, redelivered.Id);
            redeliveryChannel.Acknowledge(redelivered);
        }
        finally
        {
            if (channel != null && !disposeAttempted)
                Task.Run(channel.Dispose).Wait(s_disposeBound);
            provider.CleanUp(producer, redeliveryChannel, []);
        }
    }
}
