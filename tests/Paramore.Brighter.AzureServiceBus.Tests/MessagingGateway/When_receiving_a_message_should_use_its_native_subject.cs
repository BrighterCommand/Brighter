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

using System;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Xunit;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

[Trait("Category", "ASB")]
public class AzureServiceBusNativeSubjectReceivingTests
{
    [Theory]
    [InlineData("order-placed", false, false)]
    [InlineData("order-placed", false, true)]
    [InlineData("order-placed", true, false)]
    [InlineData("order-placed", true, true)]
    [InlineData("შეკვეთა/注文", false, false)]
    [InlineData("შეკვეთა/注文", true, true)]
    [InlineData("", false, false)]
    [InlineData("", true, true)]
    [InlineData(null, false, false)]
    [InlineData(null, true, true)]
    public async Task When_receiving_a_message_should_use_its_native_subject(
        string? subject, bool useAsync, bool useQueue)
    {
        // Arrange
        var native = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString("{}"), messageId: Id.Random().Value, subject: subject);
        await using var client = new InMemoryServiceBusClient(native);
        var factory = new AzureServiceBusConsumerFactory(client);
        var subscription = new AzureServiceBusSubscription<ASBTestCommand>(
            subscriptionName: new SubscriptionName("orders-subscription"),
            channelName: new ChannelName("orders-channel"),
            routingKey: new RoutingKey("orders"),
            makeChannels: OnMissingChannel.Assume,
            subscriptionConfiguration: new AzureServiceBusSubscriptionConfiguration { UseServiceBusQueue = useQueue });
        Message[] messages;

        // Act
        if (useAsync)
        {
            await using var consumer = factory.CreateAsync(subscription);
            messages = await consumer.ReceiveAsync(TimeSpan.FromSeconds(1));
        }
        else
        {
            using var consumer = factory.Create(subscription);
            messages = consumer.Receive(TimeSpan.FromSeconds(1));
        }

        // Assert
        var received = Assert.Single(messages);
        Assert.Equal(subject ?? string.Empty, received.Header.Subject);
        Assert.False(received.Header.Bag.ContainsKey("cloudEvents:subject"));
    }
}
