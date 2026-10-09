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
using System.Threading.Tasks;
using Xunit;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Pull;

/// <summary>
/// PR #4506 review — async (Proactor) path: one consumer, two rejections with different reasons,
/// both a DLQ and an invalid-message key configured. Each GCP producer is bound to a single topic,
/// so the rejection router must not reuse the producer it built for the first destination when the
/// second rejection chooses the other one.
/// </summary>
[Trait("Category", "GcpPubSubPull")]
[Collection("Pull")]
public class GcpConsumerRejectsTwiceRoutingAsyncTests
{
    [Fact]
    public async Task When_a_gcp_consumer_rejects_twice_with_different_reasons_should_route_each_to_its_own_destination_async()
    {
        var provider = new GcpPullMessageGatewayProvider();

        var routingKey = provider.GetOrCreateRoutingKey();
        var dlqRoutingKey = new RoutingKey($"{routingKey.Value}.DLQ");
        var invalidRoutingKey = new RoutingKey($"{routingKey.Value}.Invalid");
        var channelName = provider.GetOrCreateChannelName();

        var publication = provider.CreatePublication(routingKey);
        var subscription = provider.CreateSubscription(
            routingKey, channelName, OnMissingChannel.Create,
            deadLetterRoutingKey: dlqRoutingKey,
            invalidMessageRoutingKey: invalidRoutingKey);

        IAmAMessageProducerAsync? producer = null;
        IAmAChannelAsync? channel = null;

        try
        {
            // Arrange — two messages on the source, read through ONE channel
            producer = await provider.CreateProducerAsync(publication);
            channel = await provider.CreateChannelAsync(subscription);

            var deliveryErrorMessage = new DefaultMessageBuilder().SetTopic(routingKey).Build();
            var unacceptableMessage = new DefaultMessageBuilder().SetTopic(routingKey).Build();
            await producer.SendAsync(deliveryErrorMessage);
            await producer.SendAsync(unacceptableMessage);

            var first = await ReceiveByIdAsync(channel, deliveryErrorMessage.Id);
            var second = await ReceiveByIdAsync(channel, unacceptableMessage.Id);

            // Act — the first rejection builds the router's producer for the DLQ; the second must go elsewhere
            await channel.RejectAsync(first, new MessageRejectionReason(RejectionReason.DeliveryError, "budget exhausted"));
            await channel.RejectAsync(second, new MessageRejectionReason(RejectionReason.Unacceptable, "cannot be parsed"));

            // Assert — the DeliveryError copy is on the DLQ
            var dlqMessage = await WaitForAsync(() => provider.GetMessageFromDeadLetterQueueAsync(subscription));
            Assert.Equal(deliveryErrorMessage.Id, dlqMessage.Id);

            // Assert — the Unacceptable copy is on the invalid-message topic, not the DLQ
            var invalidMessage = await WaitForAsync(() => provider.GetMessageFromInvalidChannelAsync(subscription));
            Assert.Equal(unacceptableMessage.Id, invalidMessage.Id);
        }
        finally
        {
            await provider.CleanUpAsync(producer, channel, []);
        }
    }

    private static async Task<Message> ReceiveByIdAsync(IAmAChannelAsync channel, Id id)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < TimeSpan.FromSeconds(30))
        {
            var received = await channel.ReceiveAsync(TimeSpan.FromSeconds(5));
            if (received.Id == id)
                return received;
            if (received.Header.MessageType != MessageType.MT_NONE)
                await channel.RequeueAsync(received);
        }

        throw new TimeoutException($"Message {id} was not received from the source");
    }

    private static async Task<Message> WaitForAsync(Func<Task<Message>> read)
    {
        var message = new Message();
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < TimeSpan.FromSeconds(60))
        {
            message = await read();
            if (message.Header.MessageType != MessageType.MT_NONE)
                break;
            await Task.Delay(500);
        }

        Assert.NotEqual(MessageType.MT_NONE, message.Header.MessageType);
        return message;
    }
}
