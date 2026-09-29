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

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

public class AzureServiceBusQueueConfigurationTests
{
    [Test]
    [Arguments(false, false, false)]
    [Arguments(false, true, false)]
    [Arguments(true, false, false)]
    [Arguments(true, true, false)]
    [Arguments(false, false, true)]
    [Arguments(true, false, true)]
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
        using var consumer = new AzureServiceBusQueueConsumer(subscription, new AzureServiceBusRequeueScheduler(client),
            new AdministrationClientWrapper(client),
            new FakeServiceBusReceiverProvider(new FakeServiceBusReceiverWrapper()));

        // Act
        if (useAsync)
            await consumer.ReceiveAsync(TimeSpan.FromMilliseconds(1));
        else
            consumer.Receive(TimeSpan.FromMilliseconds(1));

        // Assert
        var queue = (await Assert.That(administration.Queues).HasSingleItem()).Value;
        await Assert.That(queue.Name).IsEqualTo("orders");
        await Assert.That(queue.RequiresSession).IsEqualTo(configuration.RequireSession);
        await Assert.That(queue.MaxDeliveryCount).IsEqualTo(configuration.MaxDeliveryCount);
        await Assert.That(queue.LockDuration).IsEqualTo(configuration.LockDuration);
        await Assert.That(queue.DefaultMessageTimeToLive).IsEqualTo(configuration.DefaultMessageTimeToLive);
        await Assert.That(queue.DeadLetteringOnMessageExpiration).IsEqualTo(configuration.DeadLetteringOnMessageExpiration);
        await Assert.That(queue.AutoDeleteOnIdle).IsEqualTo(configuration.QueueIdleBeforeDelete);
    }
}
