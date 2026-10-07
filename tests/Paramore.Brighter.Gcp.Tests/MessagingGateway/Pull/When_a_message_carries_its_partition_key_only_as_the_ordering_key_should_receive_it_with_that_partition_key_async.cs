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
using Google.Api.Gax;
using Google.Cloud.PubSub.V1;
using Google.Protobuf;
using Paramore.Brighter.Gcp.Tests.Helper;
using Xunit;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Pull;

/// <summary>
/// #4517 — a producer that predates the partition key attribute sends the partition key only as the Pub/Sub ordering
/// key. A consumer must still read the partition key from it.
/// </summary>
[Trait("Category", "GcpPubSubPull")]
[Collection("Pull")]
public class GcpPartitionKeyFromOrderingKeyAsyncTests
{
    [Fact]
    public async Task When_a_message_carries_its_partition_key_only_as_the_ordering_key_should_receive_it_with_that_partition_key_async()
    {
        var provider = new GcpPullMessageGatewayProvider();
        var routingKey = provider.GetOrCreateRoutingKey();
        var subscription = provider.CreateSubscription(routingKey, provider.GetOrCreateChannelName(), OnMissingChannel.Create);

        IAmAChannelAsync? channel = null;
        PublisherClient? publisher = null;

        try
        {
            // Arrange
            channel = await provider.CreateChannelAsync(subscription);

            publisher = await new PublisherClientBuilder
            {
                Credential = GatewayFactory.GetCredential(),
                TopicName = TopicName.FromProjectTopic(GatewayFactory.GetProjectId(), routingKey.Value),
                EmulatorDetection = EmulatorDetection.EmulatorOrProduction,
                Settings = new PublisherClient.Settings { EnableMessageOrdering = true }
            }.BuildAsync();

            var olderProducersMessage = new PubsubMessage
            {
                Data = ByteString.CopyFromUtf8("body"),
                OrderingKey = "customer-42"
            };

            // Act
            await publisher.PublishAsync(olderProducersMessage);
            var received = await channel.ReceiveAsync(TimeSpan.FromSeconds(10));
            await channel.AcknowledgeAsync(received);

            // Assert
            Assert.Equal(new PartitionKey("customer-42"), received.Header.PartitionKey);
        }
        finally
        {
            if (publisher != null)
            {
                await publisher.DisposeAsync();
            }

            await provider.CleanUpAsync(null, channel, []);
        }
    }
}
