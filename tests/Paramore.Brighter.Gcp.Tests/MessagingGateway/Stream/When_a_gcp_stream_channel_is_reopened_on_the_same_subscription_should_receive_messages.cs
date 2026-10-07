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
/// #4502 — the dispatcher reopens a channel on the same subscription instance after a Shut/Open or a
/// scale to zero and back. Once the last channel on a subscription is disposed its streaming client is
/// stopped, so the reopened channel must get a working client, not the stopped one.
/// </summary>
[Trait("Category", "GcpPubSubStream")]
[Collection("Stream")]
public class GcpStreamReopenOnSameSubscriptionTests
{
    [Fact]
    public void When_a_gcp_stream_channel_is_reopened_on_the_same_subscription_should_receive_messages()
    {
        var provider = new GcpStreamMessageGatewayProvider();
        var builder = new DefaultMessageBuilder();

        var routingKey = provider.GetOrCreateRoutingKey();
        var channelName = provider.GetOrCreateChannelName();

        var publication = provider.CreatePublication(routingKey);
        var subscription = provider.CreateSubscription(routingKey, channelName, OnMissingChannel.Create);

        IAmAMessageProducerSync? producer = null;
        IAmAChannelSync? reopenedChannel = null;

        try
        {
            // Arrange — open and dispose the only channel on the subscription, stopping its client
            producer = provider.CreateProducer(publication);
            provider.CreateChannel(subscription).Dispose();

            // Act — reopen on the same subscription instance
            reopenedChannel = provider.CreateChannel(subscription);

            var sent = builder.SetTopic(routingKey).Build();
            producer.Send(sent);
            var received = reopenedChannel.Receive(TimeSpan.FromSeconds(20));

            // Assert
            Assert.Equal(sent.Id, received.Id);
            reopenedChannel.Acknowledge(received);
        }
        finally
        {
            provider.CleanUp(producer, reopenedChannel, []);
        }
    }
}
