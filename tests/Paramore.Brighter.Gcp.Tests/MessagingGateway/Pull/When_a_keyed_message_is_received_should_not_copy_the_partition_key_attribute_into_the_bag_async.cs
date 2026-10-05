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

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Pull;

/// <summary>
/// #4517 — the partition key travels in its own attribute, which the consumer reads into
/// <see cref="MessageHeader.PartitionKey"/>. It must not also land in <see cref="MessageHeader.Bag"/>, where a forwarded
/// copy would re-publish it as if it were application data.
/// </summary>
[Trait("Category", "GcpPubSubPull")]
[Collection("Pull")]
public class GcpPartitionKeyAttributeNotInBagAsyncTests
{
    [Fact]
    public async Task When_a_keyed_message_is_received_should_not_copy_the_partition_key_attribute_into_the_bag_async()
    {
        var provider = new GcpPullMessageGatewayProvider();
        var routingKey = provider.GetOrCreateRoutingKey();
        var publication = provider.CreatePublication(routingKey);
        var subscription = provider.CreateSubscription(routingKey, provider.GetOrCreateChannelName(), OnMissingChannel.Create);

        IAmAMessageProducerAsync? producer = null;
        IAmAChannelAsync? channel = null;

        try
        {
            // Arrange
            producer = await provider.CreateProducerAsync(publication);
            channel = await provider.CreateChannelAsync(subscription);

            var sent = new DefaultMessageBuilder()
                .SetTopic(routingKey)
                .SetPartitionKey(new PartitionKey("customer-42"))
                .SetMessageId(Id.Random())
                .Build();

            // Act
            await producer.SendAsync(sent);
            var received = await channel.ReceiveAsync(TimeSpan.FromSeconds(10));
            await channel.AcknowledgeAsync(received);

            // Assert
            Assert.Equal(sent.Id, received.Id);
            Assert.False(received.Header.Bag.ContainsKey(HeaderNames.PartitionKey),
                "Header.Bag must not admit the partition key attribute");
        }
        finally
        {
            await provider.CleanUpAsync(producer, channel, []);
        }
    }
}
