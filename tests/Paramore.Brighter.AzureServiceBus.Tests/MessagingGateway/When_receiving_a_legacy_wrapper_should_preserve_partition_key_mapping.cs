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
using System.Collections.Generic;
using Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Xunit;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

[Trait("Category", "ASB")]
public class AzureServiceBusLegacyWrapperPartitionKeyTests
{
    [Theory]
    [InlineData(false, null, "")]
    [InlineData(true, "cloud-partitionkey", "cloud-partitionkey")]
    [InlineData(true, "", "")]
    [InlineData(true, null, "")]
    public void When_receiving_a_legacy_wrapper_should_preserve_partition_key_mapping(
        bool hasPartitionKey, string? partitionKey, string expectedPartitionKey)
    {
        // Arrange
        var properties = new Dictionary<string, object>();
        if (hasPartitionKey)
            properties["cloudEvents:partitionkey"] = partitionKey!;
        var brokeredMessage = new BrokeredMessage
        {
            MessageBodyValue = BinaryData.FromString("{}").ToArray(),
            Id = Id.Random().Value,
            ApplicationProperties = properties
        };
        var subscription = new AzureServiceBusSubscription<ASBTestCommand>(
            subscriptionName: new SubscriptionName("orders-subscription"),
            channelName: new ChannelName("orders-channel"),
            routingKey: new RoutingKey("orders"));
        var creator = new AzureServiceBusMessageCreator(subscription);

        // Act
        var received = creator.MapToBrighterMessage(brokeredMessage);

        // Assert
        Assert.Equal(expectedPartitionKey, received.Header.PartitionKey.Value);
    }
}
