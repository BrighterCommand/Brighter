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
using Paramore.Brighter.MessagingGateway.GcpPubSub;
using Xunit;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Stream;

/// <summary>
/// #4502 — channels on one subscription share a streaming client. A Reactor performer disposes its channel
/// twice (the pump on quit, then the performer), so a second dispose must not count as another channel
/// leaving and stop the client that the subscription's other performers are still reading from.
/// </summary>
[Trait("Category", "GcpPubSubStream")]
[Collection("Stream")]
public class GcpStreamDoubleDisposeSharedClientTests
{
    [Fact]
    public void When_a_gcp_stream_channel_is_disposed_twice_should_not_stop_another_channel_on_the_subscription()
    {
        var provider = new GcpStreamMessageGatewayProvider();
        var builder = new DefaultMessageBuilder();

        var routingKey = provider.GetOrCreateRoutingKey();
        var channelName = provider.GetOrCreateChannelName();

        var publication = provider.CreatePublication(routingKey);
        var subscription = provider.CreateSubscription(routingKey, channelName, OnMissingChannel.Create);

        IAmAMessageProducerSync? producer = null;
        IAmAChannelSync? remainingChannel = null;

        try
        {
            // Arrange — two channels on the same subscription share one streaming client
            producer = provider.CreateProducer(publication);
            var leavingChannel = provider.CreateChannel(subscription);
            remainingChannel = provider.CreateChannel(subscription);

            // Act — one channel leaves, disposed twice as a Reactor performer does
            leavingChannel.Dispose();
            leavingChannel.Dispose();

            var sent = builder.SetTopic(routingKey).Build();
            producer.Send(sent);
            var received = remainingChannel.Receive(TimeSpan.FromSeconds(20));

            // Assert — the remaining channel still receives
            Assert.Equal(sent.Id, received.Id);
            remainingChannel.Acknowledge(received);
        }
        finally
        {
            provider.CleanUp(producer, remainingChannel, []);
        }
    }
}
