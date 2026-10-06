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
using System.Linq;
using System.Threading.Tasks;
using Google.Api.Gax;
using Paramore.Brighter.Gcp.Tests.Helper;
using Paramore.Brighter.Gcp.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.GcpPubSub;
using Xunit;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Pull;

/// <summary>
/// #4517 — a message with a partition key sent through a publication that does not enable message ordering must be
/// delivered, and must arrive with its partition key. The Google client refuses an ordering key unless ordering is
/// enabled, so the partition key cannot travel only as the ordering key.
/// </summary>
[Trait("Category", "GcpPubSubPull")]
[Collection("Pull")]
public class GcpKeyedMessageOnUnorderedPublicationAsyncTests
{
    [Fact]
    public async Task When_a_keyed_message_is_sent_through_an_unordered_publication_should_receive_it_with_its_partition_key_async()
    {
        var provider = new GcpPullMessageGatewayProvider();
        var routingKey = provider.GetOrCreateRoutingKey();
        var subscription = provider.CreateSubscription(routingKey, provider.GetOrCreateChannelName(), OnMissingChannel.Create);

        IAmAMessageProducerAsync? producer = null;
        IAmAChannelAsync? channel = null;

        try
        {
            // Arrange
            var connection = new GcpMessagingGatewayConnection
            {
                Credential = GatewayFactory.GetCredential(),
                ProjectId = GatewayFactory.GetProjectId(),
                PublisherConfiguration = builder => builder.EmulatorDetection = EmulatorDetection.EmulatorOrProduction
            };

            var publication = new GcpPublication<MyCommand>
            {
                Topic = routingKey,
                MakeChannels = OnMissingChannel.Assume,
                EnableMessageOrdering = false
            };

            channel = await provider.CreateChannelAsync(subscription);
            var producers = await new GcpPubSubMessageProducerFactory(connection, [publication]).CreateAsync();
            producer = (IAmAMessageProducerAsync)producers.Values.Single();

            var partitionKey = new PartitionKey("customer-42");
            var sent = new DefaultMessageBuilder()
                .SetTopic(routingKey)
                .SetPartitionKey(partitionKey)
                .SetMessageId(Id.Random())
                .Build();

            // Act
            await producer.SendAsync(sent);
            var received = await channel.ReceiveAsync(TimeSpan.FromSeconds(10));
            await channel.AcknowledgeAsync(received);

            // Assert
            Assert.Equal(sent.Id, received.Id);
            Assert.Equal(partitionKey, received.Header.PartitionKey);
        }
        finally
        {
            await provider.CleanUpAsync(producer, channel, []);
        }
    }
}
