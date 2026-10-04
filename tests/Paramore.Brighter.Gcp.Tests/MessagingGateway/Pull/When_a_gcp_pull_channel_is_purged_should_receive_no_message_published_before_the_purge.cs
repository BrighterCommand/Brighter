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
using Xunit;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Pull;

/// <summary>
/// #4508: purging a Pull channel clears the messages already published to its subscription.
/// </summary>
[Trait("Category", "GcpPubSubPull")]
[Collection("Pull")]
public class GcpPullPurgeTests
{
    [Fact]
    public void When_a_gcp_pull_channel_is_purged_should_receive_no_message_published_before_the_purge()
    {
        var provider = new GcpPullMessageGatewayProvider();
        var builder = new DefaultMessageBuilder();

        var routingKey = provider.GetOrCreateRoutingKey();
        var channelName = provider.GetOrCreateChannelName();

        var publication = provider.CreatePublication(routingKey);
        var subscription = provider.CreateSubscription(routingKey, channelName, OnMissingChannel.Create);

        IAmAMessageProducerSync? producer = null;
        IAmAChannelSync? channel = null;

        try
        {
            // Arrange — two messages waiting on the subscription
            producer = provider.CreateProducer(publication);
            channel = provider.CreateChannel(subscription);

            producer.Send(builder.SetTopic(routingKey).SetMessageId(Id.Random()).Build());
            producer.Send(builder.SetTopic(routingKey).SetMessageId(Id.Random()).Build());

            // Act
            channel.Purge();
            var received = channel.Receive(TimeSpan.FromSeconds(10));

            // Assert
            Assert.Equal(MessageType.MT_NONE, received.Header.MessageType);
        }
        finally
        {
            provider.CleanUp(producer, channel, []);
        }
    }
}
