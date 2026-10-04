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
using System.Threading.Tasks;
using Google.Api.Gax;
using Google.Cloud.PubSub.V1;
using Paramore.Brighter.Gcp.Tests.Helper;
using Paramore.Brighter.Gcp.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.GcpPubSub;
using Xunit;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.PullOrdering;

/// <summary>
/// #4516 — a publisher configuration that assigns a new <see cref="PublisherClient.Settings"/> must not switch off the
/// message ordering an ordered publication asked for. Brighter sends every keyed message with an ordering key, so with
/// ordering switched off the Google client refuses the send.
/// </summary>
[Trait("Category", "GcpPubSubPullOrdering")]
[Collection("PullOrdering")]
public class GcpOrderedPublicationSettingsReplacedAsyncTests
{
    [Fact]
    public async Task When_a_publisher_configuration_replaces_the_settings_of_an_ordered_publication_should_still_send_in_order_async()
    {
        var provider = new GcpPullOrderingMessageGatewayProvider();
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
                PublisherConfiguration = builder =>
                {
                    builder.EmulatorDetection = EmulatorDetection.EmulatorOrProduction;
                    builder.Settings = new PublisherClient.Settings();
                }
            };

            var publication = new GcpPublication<MyCommand>
            {
                Topic = routingKey,
                MakeChannels = OnMissingChannel.Assume,
                EnableMessageOrdering = true
            };

            channel = await provider.CreateChannelAsync(subscription);
            var producers = await new GcpPubSubMessageProducerFactory(connection, [publication]).CreateAsync();
            producer = (IAmAMessageProducerAsync)producers.Values.Single();

            var partitionKey = new PartitionKey(Uuid.NewAsString());
            var sent = new List<Message>
            {
                new DefaultMessageBuilder().SetTopic(routingKey).SetPartitionKey(partitionKey).SetMessageId(Id.Random()).Build(),
                new DefaultMessageBuilder().SetTopic(routingKey).SetPartitionKey(partitionKey).SetMessageId(Id.Random()).Build(),
                new DefaultMessageBuilder().SetTopic(routingKey).SetPartitionKey(partitionKey).SetMessageId(Id.Random()).Build()
            };

            // Act
            foreach (var message in sent)
            {
                await producer.SendAsync(message);
            }

            var received = new List<Message>();
            for (var i = 0; i < sent.Count; i++)
            {
                var message = await channel.ReceiveAsync(TimeSpan.FromSeconds(15));
                received.Add(message);
                await channel.AcknowledgeAsync(message);
            }

            // Assert
            Assert.Equal(sent.Select(m => m.Id), received.Select(m => m.Id));
        }
        finally
        {
            await provider.CleanUpAsync(producer, channel, []);
        }
    }
}
