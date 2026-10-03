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
using Paramore.Brighter.AzureServiceBus.Tests.Fakes;
using Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Xunit;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

public class AzureServiceBusLegacySessionReceiverTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_receiving_a_legacy_session_wrapper_should_keep_it_open_until_settlement(bool useAsync)
    {
        // Arrange
        var receiver = new FakeServiceBusReceiverWrapper
        {
            MessageQueue =
            [
                new BrokeredMessage
                {
                    Id = Guid.NewGuid().ToString(),
                    LockToken = Guid.NewGuid().ToString(),
                    MessageBodyValue = "{}"u8.ToArray(),
                    ApplicationProperties = new Dictionary<string, object>()
                }
            ]
        };
        var subscription = new AzureServiceBusSubscription<ASBTestCommand>(
            channelName: new ChannelName("locks"), routingKey: new RoutingKey("locks"),
            makeChannels: OnMissingChannel.Assume,
            subscriptionConfiguration: new AzureServiceBusSubscriptionConfiguration { RequireSession = true });
        await using var consumer = new AzureServiceBusTopicConsumer(subscription, new FakeMessageProducer(),
            new FakeAdministrationClient(), new FakeServiceBusReceiverProvider(receiver));

        // Act
        var message = Assert.Single(useAsync ? await consumer.ReceiveAsync() : consumer.Receive());
        var closedBeforeSettlement = receiver.IsClosedOrClosing;
        if (useAsync) await consumer.AcknowledgeAsync(message);
        else consumer.Acknowledge(message);

        // Assert
        Assert.False(closedBeforeSettlement);
        Assert.True(receiver.IsClosedOrClosing);
    }
}
