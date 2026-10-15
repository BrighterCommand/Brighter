#region Licence

/* The MIT License (MIT)
Copyright © 2026 gabisonia

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
using Paramore.Brighter.Gcp.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.GcpPubSub;
using Xunit;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Stream;

[Trait("Category", "GcpPubSubStream")]
[Trait("Category", "GcpPubSubPurge")]
[Collection("Stream")]
public class GcpStreamPurgeSaturatedAsyncTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(-5)]
    public async Task When_purging_a_saturated_stream_should_clear_the_backlog_and_deliver_new_messages_async(int clientLagMinutes)
    {
        //Arrange
        var provider = new GcpStreamMessageGatewayProvider();
        var builder = new DefaultMessageBuilder();
        var routingKey = provider.GetOrCreateRoutingKey();
        var channelName = provider.GetOrCreateChannelName();
        var publication = provider.CreatePublication(routingKey);
        var subscription = new GcpPubSubSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(channelName.Value),
            channelName: channelName,
            routingKey: routingKey,
            bufferSize: 1,
            noOfPerformers: 1,
            messagePumpType: MessagePumpType.Proactor,
            makeChannels: OnMissingChannel.Create,
            timeProvider: new LaggingTimeProvider(TimeSpan.FromMinutes(clientLagMinutes)),
            subscriptionMode: SubscriptionMode.Stream);

        IAmAMessageProducerAsync? producer = null;
        IAmAChannelAsync? channel = null;

        try
        {
            producer = await provider.CreateProducerAsync(publication);
            channel = await provider.CreateChannelAsync(subscription);

            var heldMessage = builder.SetTopic(routingKey).SetMessageId(Id.Random()).Build();
            await producer.SendAsync(heldMessage);
            var heldDelivery = await channel.ReceiveAsync(TimeSpan.FromSeconds(20));
            Assert.Equal(heldMessage.Id, heldDelivery.Id);

            for (var i = 0; i < 20; i++)
            {
                await producer.SendAsync(builder.SetTopic(routingKey).SetMessageId(Id.Random()).Build());
            }

            //Act
            await channel.PurgeAsync();
            await channel.AcknowledgeAsync(heldDelivery);
            var afterPurge = await channel.ReceiveAsync(TimeSpan.FromSeconds(10));

            //Assert
            Assert.True(afterPurge.IsEmpty);

            var newMessage = builder.SetTopic(routingKey).SetMessageId(Id.Random()).Build();
            await producer.SendAsync(newMessage);
            var afterPublish = await GcpPurgeTestReceiver.ReceiveAsync(channel);

            Assert.Equal(newMessage.Id, afterPublish.Id);
            await channel.AcknowledgeAsync(afterPublish);
        }
        finally
        {
            await provider.CleanUpAsync(producer, channel, []);
        }
    }
}

