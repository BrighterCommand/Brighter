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
public class AzureServiceBusDiagnosticIdFallbackTests
{
    private const string DIAGNOSTIC_ID = "00-a67a6a91e19aaba2a6fb61a533ad84e1-05c07e9784472964-01";
    private const string UNSAMPLED_DIAGNOSTIC_ID = "00-a67a6a91e19aaba2a6fb61a533ad84e1-05c07e9784472964-00";

    [Theory]
    [InlineData(DIAGNOSTIC_ID, false, false)]
    [InlineData(DIAGNOSTIC_ID, false, true)]
    [InlineData(DIAGNOSTIC_ID, true, false)]
    [InlineData(DIAGNOSTIC_ID, true, true)]
    [InlineData(UNSAMPLED_DIAGNOSTIC_ID, true, false)]
    public async Task When_receiving_a_message_should_use_a_valid_diagnostic_id_when_cloud_events_traceparent_is_absent(
        string diagnosticId, bool useAsync, bool useQueue)
    {
        // Arrange
        var native = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString("{}"), messageId: Id.Random().Value,
            properties: new Dictionary<string, object> { ["Diagnostic-Id"] = diagnosticId });
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
        Assert.Equal(diagnosticId, received.Header.TraceParent?.Value);
        Assert.Equal(diagnosticId, received.Header.Bag["Diagnostic-Id"]);
    }
}
