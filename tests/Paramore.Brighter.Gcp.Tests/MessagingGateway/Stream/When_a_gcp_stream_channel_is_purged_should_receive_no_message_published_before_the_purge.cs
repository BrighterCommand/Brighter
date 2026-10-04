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

using System;
using Paramore.Brighter.Gcp.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.GcpPubSub;
using Xunit;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Stream;

/// <summary>
/// #4508: purging a Stream channel clears the messages published before the purge, including those
/// the streaming client has already delivered into the consumer's local buffer.
/// </summary>
[Trait("Category", "GcpPubSubStream")]
[Collection("Stream")]
public class GcpStreamPurgeTests
{
    [Fact]
    public void When_a_gcp_stream_channel_is_purged_should_receive_no_message_published_before_the_purge()
    {
        var provider = new GcpStreamMessageGatewayProvider();
        var builder = new DefaultMessageBuilder();

        var routingKey = provider.GetOrCreateRoutingKey();
        var channelName = provider.GetOrCreateChannelName();

        var publication = provider.CreatePublication(routingKey);
        // A buffer of 3 lets the streaming client hold the published messages locally
        var subscription = new GcpPubSubSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: channelName,
            routingKey: routingKey,
            bufferSize: 3,
            messagePumpType: MessagePumpType.Reactor,
            makeChannels: OnMissingChannel.Create,
            subscriptionMode: SubscriptionMode.Stream);

        IAmAMessageProducerSync? producer = null;
        IAmAChannelSync? channel = null;

        try
        {
            // Arrange — three messages published; taking the first shows the client is streaming,
            // so the other two are in its local buffer
            producer = provider.CreateProducer(publication);
            channel = provider.CreateChannel(subscription);

            producer.Send(builder.SetTopic(routingKey).SetMessageId(Id.Random()).Build());
            producer.Send(builder.SetTopic(routingKey).SetMessageId(Id.Random()).Build());
            producer.Send(builder.SetTopic(routingKey).SetMessageId(Id.Random()).Build());

            var first = channel.Receive(TimeSpan.FromSeconds(20));
            channel.Acknowledge(first);

            // Act
            channel.Purge();
            var received = channel.Receive(TimeSpan.FromSeconds(10));

            // Assert
            Assert.Equal(MessageType.MT_NONE, received.Header.MessageType);
        }
        finally
        {
            provider.CleanUp(producer, channel, []);
        }
    }
}
