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
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.GcpPubSub;
using Xunit;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Stream;

/// <summary>
/// #4479 scope — the dispose half of #4449 — async (Proactor) path. A message received but never
/// settled (neither acknowledged, rejected nor requeued) keeps its <c>SubscriberClient</c> callback
/// parked; disposing the channel must not wait on it, and the message must stay redeliverable.
/// </summary>
[Trait("Category", "GcpPubSubStream")]
[Collection("Stream")]
public class GcpStreamDisposeWithUnsettledMessageAsyncTests
{
    private static readonly TimeSpan s_disposeBound = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task When_a_gcp_stream_channel_is_disposed_holding_an_unsettled_message_async_should_return_promptly()
    {
        var provider = new GcpStreamMessageGatewayProvider();
        var builder = new DefaultMessageBuilder();

        var routingKey = provider.GetOrCreateRoutingKey();
        var channelName = provider.GetOrCreateChannelName();

        var publication = provider.CreatePublication(routingKey);
        var subscription = provider.CreateSubscription(routingKey, channelName, OnMissingChannel.Create);

        IAmAMessageProducerAsync? producer = null;
        IAmAChannelAsync? channel = null;
        IAmAChannelAsync? redeliveryChannel = null;
        var disposeAttempted = false;

        try
        {
            // Arrange — receive a message and leave it unsettled
            producer = await provider.CreateProducerAsync(publication);
            channel = await provider.CreateChannelAsync(subscription);

            await producer.SendAsync(builder.SetTopic(routingKey).Build());
            var first = await channel.ReceiveAsync(TimeSpan.FromSeconds(20));
            Assert.NotEqual(MessageType.MT_NONE, first.Header.MessageType);

            // Act
            disposeAttempted = true;
            var disposing = channel.DisposeAsync().AsTask();
            var disposed = await Task.WhenAny(disposing, Task.Delay(s_disposeBound)) == disposing;

            // Assert — dispose returns promptly, and the unsettled message is still redeliverable
            Assert.True(disposed, $"Disposing the stream channel did not return within {s_disposeBound}");

            // a fresh subscription instance, so the factory builds a new stream consumer
            redeliveryChannel = await provider.CreateChannelAsync(
                provider.CreateSubscription(routingKey, channelName, OnMissingChannel.Assume));
            var redelivered = await redeliveryChannel.ReceiveAsync(TimeSpan.FromSeconds(20));
            Assert.Equal(first.Id, redelivered.Id);
            await redeliveryChannel.AcknowledgeAsync(redelivered);
        }
        finally
        {
            if (channel != null && !disposeAttempted)
            {
                var disposing = channel.DisposeAsync().AsTask();
                await Task.WhenAny(disposing, Task.Delay(s_disposeBound));
            }
            await provider.CleanUpAsync(producer, redeliveryChannel, []);
        }
    }
}
