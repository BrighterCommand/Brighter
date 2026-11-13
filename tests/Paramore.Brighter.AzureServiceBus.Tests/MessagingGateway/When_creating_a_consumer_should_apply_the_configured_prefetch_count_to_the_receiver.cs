#region Licence
/* The MIT License (MIT)
Copyright © 2026 Anurag Mishra <am6840957@gmail.com>

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

public class AzureServiceBusPrefetchCountTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task When_creating_a_consumer_should_apply_the_configured_prefetch_count_to_the_receiver(
        bool useQueue, bool requireSession)
    {
        // Arrange
        var lockDuration = TimeSpan.FromMinutes(1);
        ServiceBusReceiver receiver = requireSession
            ? new InMemoryLockingServiceBusSessionReceiver(new InMemoryLockingServiceBusReceiver(0, lockDuration), lockDuration)
            : new InMemoryLockingServiceBusReceiver(0, lockDuration);
        await using var client = new RecordingReceiverOptionsServiceBusClient(receiver);
        var factory = new AzureServiceBusConsumerFactory(client, new AzureServiceBusPublication());
        var subscription = new AzureServiceBusSubscription(new SubscriptionName("prefetch"),
            new ChannelName("prefetch"), new RoutingKey("prefetch"), requestType: typeof(ASBTestCommand),
            messagePumpType: MessagePumpType.Proactor, makeChannels: OnMissingChannel.Assume,
            subscriptionConfiguration: new AzureServiceBusSubscriptionConfiguration
            {
                UseServiceBusQueue = useQueue,
                RequireSession = requireSession,
                PrefetchCount = 20
            });

        // Act
        using var consumer = factory.Create(subscription);
        var messages = consumer.Receive(TimeSpan.FromMilliseconds(10));

        // Assert
        Assert.Empty(messages);
        if (requireSession)
        {
            Assert.NotNull(client.SessionReceiverOptions);
            Assert.Equal(20, client.SessionReceiverOptions!.PrefetchCount);
            Assert.Equal(ServiceBusReceiveMode.PeekLock, client.SessionReceiverOptions.ReceiveMode);
        }
        else
        {
            Assert.NotNull(client.ReceiverOptions);
            Assert.Equal(20, client.ReceiverOptions!.PrefetchCount);
            Assert.Equal(ServiceBusReceiveMode.PeekLock, client.ReceiverOptions.ReceiveMode);
        }
    }
}
