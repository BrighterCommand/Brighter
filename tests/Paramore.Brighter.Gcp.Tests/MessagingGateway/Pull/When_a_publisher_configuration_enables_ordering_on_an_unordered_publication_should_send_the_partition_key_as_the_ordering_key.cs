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
using System.Diagnostics;
using System.Linq;
using Google.Api.Gax;
using Google.Cloud.PubSub.V1;
using Paramore.Brighter.Gcp.Tests.Helper;
using Paramore.Brighter.Gcp.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.GcpPubSub;
using Xunit;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Pull;

/// <summary>
/// #4517 — a publisher configuration may enable message ordering on a publication that does not ask for it. The
/// producer must then still send the partition key as the Pub/Sub ordering key, so that the ordering the configuration
/// asked for is kept.
/// </summary>
[Trait("Category", "GcpPubSubPull")]
[Collection("Pull")]
public class GcpOrderingEnabledByConfigurationTests
{
    [Fact]
    public void When_a_publisher_configuration_enables_ordering_on_an_unordered_publication_should_send_the_partition_key_as_the_ordering_key()
    {
        var provider = new GcpPullMessageGatewayProvider();
        var routingKey = provider.GetOrCreateRoutingKey();
        var channelName = provider.GetOrCreateChannelName();
        var subscription = provider.CreateSubscription(routingKey, channelName, OnMissingChannel.Create);

        IAmAMessageProducerSync? producer = null;
        IAmAChannelSync? channel = null;

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
                    builder.Settings.EnableMessageOrdering = true;
                },
                SubscriptionManagerConfiguration = builder => builder.EmulatorDetection = EmulatorDetection.EmulatorOrProduction
            };

            var publication = new GcpPublication<MyCommand>
            {
                Topic = routingKey,
                MakeChannels = OnMissingChannel.Assume,
                EnableMessageOrdering = false
            };

            channel = provider.CreateChannel(subscription);
            var producers = new GcpPubSubMessageProducerFactory(connection, [publication]).Create();
            producer = (IAmAMessageProducerSync)producers.Values.Single();

            var partitionKey = new PartitionKey("customer-42");
            var sent = new DefaultMessageBuilder()
                .SetTopic(routingKey)
                .SetPartitionKey(partitionKey)
                .SetMessageId(Id.Random())
                .Build();

            // Act
            producer.Send(sent);
            var received = PullOne(connection, channelName);

            // Assert
            Assert.NotNull(received);
            Assert.Equal(partitionKey.Value, received.Message.OrderingKey);
        }
        finally
        {
            provider.CleanUp(producer, channel, []);
        }
    }

    // Read the raw Pub/Sub message, as the Brighter message does not show which Pub/Sub field carried the key
    private static ReceivedMessage? PullOne(GcpMessagingGatewayConnection connection, ChannelName channelName)
    {
        var client = connection.GetOrCreateSubscriberServiceApiClient();
        var subscriptionName = Google.Cloud.PubSub.V1.SubscriptionName.FromProjectSubscription(connection.ProjectId, channelName.Value);

        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < TimeSpan.FromSeconds(10))
        {
            var response = client.Pull(subscriptionName, maxMessages: 1);
            var received = response.ReceivedMessages.FirstOrDefault();
            if (received != null)
            {
                client.Acknowledge(subscriptionName, [received.AckId]);
                return received;
            }
        }

        return null;
    }
}
