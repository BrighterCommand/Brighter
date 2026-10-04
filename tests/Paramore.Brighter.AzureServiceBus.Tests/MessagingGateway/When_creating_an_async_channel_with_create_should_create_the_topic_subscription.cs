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
using Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Xunit;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

public class AzureServiceBusAsyncChannelProvisioningTests
{
    [Fact]
    public void When_creating_an_async_channel_with_create_should_create_the_topic_subscription()
    {
        // Arrange
        var administrationClient = new InMemoryServiceBusAdministrationClient();
        var factory = new AzureServiceBusChannelFactory(
            new AzureServiceBusConsumerFactory(new InMemoryServiceBusClientProvider(administrationClient)));

        var subscription = new AzureServiceBusSubscription<ASBTestCommand>(
            new SubscriptionName("orders-subscription"),
            new ChannelName("orders-channel"),
            new RoutingKey("orders"),
            messagePumpType: MessagePumpType.Proactor,
            timeOut: TimeSpan.FromSeconds(1),
            makeChannels: OnMissingChannel.Create);

        // Act
        using var channel = factory.CreateAsyncChannel(subscription);

        // Assert
        Assert.Contains(("orders", "orders-channel"), administrationClient.CreatedSubscriptions);
    }
}
