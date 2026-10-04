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
using System.Threading.Tasks;
using Paramore.Brighter.Gcp.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.GcpPubSub;
using Xunit;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Stream;

/// <summary>
/// #4508, async (Proactor) path: purging a Stream channel clears the messages published before the
/// purge, including those the streaming client has already delivered into the consumer's local buffer.
/// </summary>
[Trait("Category", "GcpPubSubStream")]
[Collection("Stream")]
public class GcpStreamPurgeAsyncTests
{
    [Fact]
    public async Task When_a_gcp_stream_channel_is_purged_should_receive_no_message_published_before_the_purge_async()
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
            messagePumpType: MessagePumpType.Proactor,
            makeChannels: OnMissingChannel.Create,
            subscriptionMode: SubscriptionMode.Stream);

        IAmAMessageProducerAsync? producer = null;
        IAmAChannelAsync? channel = null;

        try
        {
            // Arrange — three messages published; taking the first shows the client is streaming,
            // so the other two are in its local buffer
            producer = await provider.CreateProducerAsync(publication);
            channel = await provider.CreateChannelAsync(subscription);

            await producer.SendAsync(builder.SetTopic(routingKey).SetMessageId(Id.Random()).Build());
            await producer.SendAsync(builder.SetTopic(routingKey).SetMessageId(Id.Random()).Build());
            await producer.SendAsync(builder.SetTopic(routingKey).SetMessageId(Id.Random()).Build());

            var first = await channel.ReceiveAsync(TimeSpan.FromSeconds(20));
            await channel.AcknowledgeAsync(first);

            // Act
            await channel.PurgeAsync();
            var received = await channel.ReceiveAsync(TimeSpan.FromSeconds(10));

            // Assert
            Assert.Equal(MessageType.MT_NONE, received.Header.MessageType);
        }
        finally
        {
            await provider.CleanUpAsync(producer, channel, []);
        }
    }
}
