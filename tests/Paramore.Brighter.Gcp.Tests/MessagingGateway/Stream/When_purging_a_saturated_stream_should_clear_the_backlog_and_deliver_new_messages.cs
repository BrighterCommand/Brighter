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
using Paramore.Brighter.Gcp.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.GcpPubSub;
using Xunit;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Stream;

[Trait("Category", "GcpPubSubStream")]
[Trait("Category", "GcpPubSubPurge")]
[Collection("Stream")]
public class GcpStreamPurgeSaturatedTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(-5)]
    public void When_purging_a_saturated_stream_should_clear_the_backlog_and_deliver_new_messages(int clientLagMinutes)
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
            messagePumpType: MessagePumpType.Reactor,
            makeChannels: OnMissingChannel.Create,
            timeProvider: new LaggingTimeProvider(TimeSpan.FromMinutes(clientLagMinutes)),
            subscriptionMode: SubscriptionMode.Stream);

        IAmAMessageProducerSync? producer = null;
        IAmAChannelSync? channel = null;

        try
        {
            producer = provider.CreateProducer(publication);
            channel = provider.CreateChannel(subscription);

            var heldMessage = builder.SetTopic(routingKey).SetMessageId(Id.Random()).Build();
            producer.Send(heldMessage);
            var heldDelivery = channel.Receive(TimeSpan.FromSeconds(20));
            Assert.Equal(heldMessage.Id, heldDelivery.Id);

            for (var i = 0; i < 20; i++)
            {
                producer.Send(builder.SetTopic(routingKey).SetMessageId(Id.Random()).Build());
            }

            //Act
            channel.Purge();
            channel.Acknowledge(heldDelivery);
            var afterPurge = channel.Receive(TimeSpan.FromSeconds(10));

            //Assert
            Assert.True(afterPurge.IsEmpty);

            var newMessage = builder.SetTopic(routingKey).SetMessageId(Id.Random()).Build();
            producer.Send(newMessage);
            var afterPublish = GcpPurgeTestReceiver.Receive(channel);

            Assert.Equal(newMessage.Id, afterPublish.Id);
            channel.Acknowledge(afterPublish);
        }
        finally
        {
            provider.CleanUp(producer, channel, []);
        }
    }
}

