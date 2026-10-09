#region Licence
/* The MIT License (MIT)
Copyright © 2026 Avtandil Ushikishvili <a.ushikishvili@gmail.com>

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
public class AzureServiceBusInvalidDiagnosticIdTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-traceparent")]
    [InlineData("|a67a6a91e19aaba2a6fb61a533ad84e1.1.")]
    [InlineData("00-00000000000000000000000000000000-05c07e9784472964-01")]
    [InlineData("00-a67a6a91e19aaba2a6fb61a533ad84e1-0000000000000000-01")]
    [InlineData("00-a67a6a91e19aaba2a6fb61a533ad84e1-05c07e9784472964-zz")]
    [InlineData(123)]
    public async Task When_receiving_an_invalid_diagnostic_id_should_leave_the_traceparent_empty(object? diagnosticId)
    {
        // Arrange
        var native = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString("{}"), messageId: Id.Random().Value,
            properties: new Dictionary<string, object> { ["Diagnostic-Id"] = diagnosticId! });
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
        Assert.Equal(string.Empty, received.Header.TraceParent?.Value);
        Assert.Equal(diagnosticId, received.Header.Bag["Diagnostic-Id"]);
    }
}
