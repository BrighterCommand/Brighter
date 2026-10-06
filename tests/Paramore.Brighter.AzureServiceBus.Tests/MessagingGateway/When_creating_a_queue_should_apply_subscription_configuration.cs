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
using Paramore.Brighter.AzureServiceBus.Tests.Fakes;
using Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers;
using Xunit;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

public class AzureServiceBusQueueConfigurationTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    public async Task When_creating_a_queue_should_apply_subscription_configuration(
        bool useAsync, bool requireSession, bool useDefaults)
    {
        // Arrange
        var configuration = new AzureServiceBusSubscriptionConfiguration
        {
            UseServiceBusQueue = true,
            RequireSession = requireSession
        };
        if (!useDefaults)
        {
            configuration.MaxDeliveryCount = 7;
            configuration.LockDuration = TimeSpan.FromMinutes(2);
            configuration.DefaultMessageTimeToLive = TimeSpan.FromHours(6);
            configuration.DeadLetteringOnMessageExpiration = requireSession;
            configuration.QueueIdleBeforeDelete = TimeSpan.FromMinutes(15);
        }
        var subscription = new AzureServiceBusSubscription(
            new SubscriptionName("orders-subscription"), new ChannelName("orders-channel"), new RoutingKey("orders"),
            requestType: typeof(Command),
            messagePumpType: useAsync ? MessagePumpType.Proactor : MessagePumpType.Reactor,
            makeChannels: OnMissingChannel.Create, subscriptionConfiguration: configuration);
        var administration = new InMemoryServiceBusAdministrationClient();
        await using var client = new InMemoryServiceBusClient(ServiceBusModelFactory.ServiceBusReceivedMessage(), administration);
        using var producer = new InMemoryMessageProducer(new InternalBus());
        using var consumer = new AzureServiceBusQueueConsumer(subscription, producer,
            new AdministrationClientWrapper(client),
            new FakeServiceBusReceiverProvider(new FakeServiceBusReceiverWrapper()));

        // Act
        if (useAsync)
            await consumer.ReceiveAsync(TimeSpan.FromMilliseconds(1));
        else
            consumer.Receive(TimeSpan.FromMilliseconds(1));

        // Assert
        var queue = Assert.Single(administration.Queues).Value;
        Assert.Equal("orders", queue.Name);
        Assert.Equal(configuration.RequireSession, queue.RequiresSession);
        Assert.Equal(configuration.MaxDeliveryCount, queue.MaxDeliveryCount);
        Assert.Equal(configuration.LockDuration, queue.LockDuration);
        Assert.Equal(configuration.DefaultMessageTimeToLive, queue.DefaultMessageTimeToLive);
        Assert.Equal(configuration.DeadLetteringOnMessageExpiration, queue.DeadLetteringOnMessageExpiration);
        Assert.Equal(configuration.QueueIdleBeforeDelete, queue.AutoDeleteOnIdle);
    }
}
