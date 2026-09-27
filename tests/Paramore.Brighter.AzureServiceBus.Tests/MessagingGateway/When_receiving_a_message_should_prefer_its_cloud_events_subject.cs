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
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Xunit;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

[Trait("Category", "ASB")]
public class AzureServiceBusCloudEventsSubjectPrecedenceTests
{
    [Theory]
    [InlineData("cloud-subject", "native-subject", "cloud-subject")]
    [InlineData("cloud-subject", null, "cloud-subject")]
    [InlineData("cloud-subject", "", "cloud-subject")]
    [InlineData("", "native-subject", "")]
    [InlineData(null, "native-subject", "")]
    public async Task When_receiving_a_message_should_prefer_its_cloud_events_subject(
        string? cloudSubject, string? nativeSubject, string expectedSubject)
    {
        // Arrange
        var native = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString("{}"), messageId: Id.Random().Value, subject: nativeSubject,
            properties: new Dictionary<string, object> { ["cloudEvents:subject"] = cloudSubject! });
        await using var client = new InMemoryServiceBusClient(native);
        var factory = new AzureServiceBusConsumerFactory(client);
        var subscription = new AzureServiceBusSubscription<ASBTestCommand>(
            subscriptionName: new SubscriptionName("orders-subscription"),
            channelName: new ChannelName("orders-channel"),
            routingKey: new RoutingKey("orders"),
            makeChannels: OnMissingChannel.Assume);
        await using var consumer = factory.CreateAsync(subscription);

        // Act
        var messages = await consumer.ReceiveAsync(TimeSpan.FromSeconds(1));

        // Assert
        var received = Assert.Single(messages);
        Assert.Equal(expectedSubject, received.Header.Subject);
        Assert.Equal(cloudSubject, received.Header.Bag["cloudEvents:subject"]);
    }
}
