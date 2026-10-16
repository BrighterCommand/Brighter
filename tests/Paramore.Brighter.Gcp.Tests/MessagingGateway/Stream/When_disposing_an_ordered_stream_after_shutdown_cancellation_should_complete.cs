#region Licence
/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia

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
using Google.Cloud.PubSub.V1;
using Paramore.Brighter.Gcp.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.GcpPubSub;
using Xunit;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Stream;

/// <summary>
/// Forces the real subscriber client's shutdown deadline to expire after an ordered delivery.
/// </summary>
[Trait("Category", "GcpPubSubStreamOrdering")]
[Collection("StreamOrdering")]
public class GcpStreamOrderingShutdownCancellationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_disposing_an_ordered_stream_after_shutdown_cancellation_should_complete(
        bool acknowledgeBeforeDisposing)
    {
        // Arrange
        var provider = new GcpStreamOrderingMessageGatewayProvider();
        var routingKey = provider.GetOrCreateRoutingKey();
        var channelName = provider.GetOrCreateChannelName();
        var subscription = new GcpPubSubSubscription(
            subscriptionName: new SubscriptionName(channelName.Value),
            channelName: channelName,
            routingKey: routingKey,
            requestType: typeof(MyCommand),
            bufferSize: 2,
            messagePumpType: MessagePumpType.Reactor,
            makeChannels: OnMissingChannel.Create,
            subscriptionMode: SubscriptionMode.Stream,
            enableMessageOrdering: true,
            streamingConfiguration: builder =>
            {
                builder.ClientCount = 1;
                builder.Settings = new SubscriberClient.Settings { MaxTotalAckExtension = TimeSpan.FromMilliseconds(1) };
            });

        IAmAMessageProducerSync? producer = null;
        IAmAChannelSync? channel = null;
        try
        {
            producer = provider.CreateProducer(provider.CreatePublication(routingKey));
            channel = provider.CreateChannel(subscription);
            var message = new DefaultMessageBuilder()
                .SetTopic(routingKey)
                .SetPartitionKey(new PartitionKey("shutdown-key"))
                .Build();
            producer.Send(message);
            var received = channel.Receive(TimeSpan.FromSeconds(20));
            Assert.False(received.IsEmpty);
            Assert.Equal(message.Id, received.Id);
            if (acknowledgeBeforeDisposing)
                channel.Acknowledge(received);

            // Act
            var error = await Record.ExceptionAsync(() =>
                Task.Run(channel.Dispose).WaitAsync(TimeSpan.FromSeconds(60)));

            // Assert
            Assert.Null(error);
            channel.Dispose();
        }
        finally
        {
            try
            {
                if (channel != null)
                    await Task.Run(channel.Dispose).WaitAsync(TimeSpan.FromSeconds(60));
            }
            finally
            {
                provider.CleanUp(producer, null, []);
            }
        }
    }
}
