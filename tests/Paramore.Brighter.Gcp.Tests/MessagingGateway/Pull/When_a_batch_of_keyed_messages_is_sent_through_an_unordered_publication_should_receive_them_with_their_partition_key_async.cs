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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Api.Gax;
using Paramore.Brighter.Gcp.Tests.Helper;
using Paramore.Brighter.Gcp.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.GcpPubSub;
using Xunit;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Pull;

/// <summary>
/// #4517 — the bulk send path must, like the single send, deliver keyed messages through a publication that does not
/// enable message ordering, and they must arrive with their partition key.
/// </summary>
[Trait("Category", "GcpPubSubPull")]
[Collection("Pull")]
public class GcpKeyedBatchOnUnorderedPublicationAsyncTests
{
    [Fact]
    public async Task When_a_batch_of_keyed_messages_is_sent_through_an_unordered_publication_should_receive_them_with_their_partition_key_async()
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
            var bulkProducer = (IAmABulkMessageProducerAsync)producer;

            var partitionKey = new PartitionKey("customer-42");
            var sent = new List<Message>
            {
                new DefaultMessageBuilder().SetTopic(routingKey).SetPartitionKey(partitionKey).SetMessageId(Id.Random()).Build(),
                new DefaultMessageBuilder().SetTopic(routingKey).SetPartitionKey(partitionKey).SetMessageId(Id.Random()).Build()
            };

            // Act
            foreach (var batch in await bulkProducer.CreateBatchesAsync(sent, CancellationToken.None))
            {
                await bulkProducer.SendAsync(batch, CancellationToken.None);
            }

            var received = new List<Message>();
            for (var i = 0; i < sent.Count; i++)
            {
                var message = await channel.ReceiveAsync(TimeSpan.FromSeconds(10));
                received.Add(message);
                await channel.AcknowledgeAsync(message);
            }

            // Assert
            Assert.Equal(sent.Select(m => m.Id).OrderBy(id => id.Value), received.Select(m => m.Id).OrderBy(id => id.Value));
            Assert.All(received, message => Assert.Equal(partitionKey, message.Header.PartitionKey));
        }
        finally
        {
            await provider.CleanUpAsync(producer, channel, []);
        }
    }
}
