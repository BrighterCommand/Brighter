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
using Xunit;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Pull;

/// <summary>
/// R-28 — ADR 0077 "Where each transport reads its counter", GCP stream row — async (Proactor) path.
/// The GCP parser must never admit the <c>googclient_deliveryattempt</c> attribute into
/// <c>Header.Bag</c>: a routed copy re-publishes every non-ignored bag entry (Parser.cs), so an
/// admitted attribute would leak forward as if it were application data. The emulator accepts an
/// explicitly published <c>googclient_deliveryattempt</c> attribute (5.5c(ii) measurement), so this
/// publishes it explicitly rather than relying on delivery to inject one.
/// </summary>
[Trait("Category", "GcpPubSubPull")]
[Collection("Pull")]
public class GcpPullDeliveryAttemptAttributeAsyncTests
{
    private const string DeliveryAttemptAttrKey = "googclient_deliveryattempt";

    [Fact]
    public async Task When_a_gcp_pull_message_carries_delivery_attempt_attribute_async_should_not_copy_it_into_bag()
    {
        var provider = new GcpPullMessageGatewayProvider();
        var builder = new DefaultMessageBuilder();

        var routingKey = provider.GetOrCreateRoutingKey();
        var channelName = provider.GetOrCreateChannelName();

        var publication = provider.CreatePublication(routingKey);
        var subscription = provider.CreateSubscription(
            routingKey, channelName, OnMissingChannel.Create);

        IAmAMessageProducerAsync? producer = null;
        IAmAChannelAsync? channel = null;

        try
        {
            // Arrange
            producer = await provider.CreateProducerAsync(publication);
            channel = await provider.CreateChannelAsync(subscription);

            var message = builder.SetTopic(routingKey).Build();
            message.Header.Bag[DeliveryAttemptAttrKey] = "1";

            // Act
            await producer.SendAsync(message);
            var received = await channel.ReceiveAsync(TimeSpan.FromSeconds(10));
            Assert.NotEqual(MessageType.MT_NONE, received.Header.MessageType);

            // Assert
            Assert.False(received.Header.Bag.ContainsKey(DeliveryAttemptAttrKey),
                "Header.Bag must not admit googclient_deliveryattempt");
        }
        finally
        {
            await provider.CleanUpAsync(producer, channel, []);
        }
    }
}
