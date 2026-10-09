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
using Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Xunit;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

public class AzureServiceBusExpiredLockTests
{
    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 0)]
    [InlineData(false, 1500)]
    [InlineData(true, 1500)]
    public async Task When_buffered_message_lock_expires_should_skip_dispatch(bool useAsync, int renewalMilliseconds)
    {
        // Arrange
        var receiver = new InMemoryLockingServiceBusReceiver(2, TimeSpan.FromSeconds(2));
        await using var client = new InMemoryLockingServiceBusClient(receiver);
        var subscription = new AzureServiceBusSubscription<ASBTestCommand>(
            channelName: new ChannelName("locks"), routingKey: new RoutingKey("locks"),
            bufferSize: 2, makeChannels: OnMissingChannel.Assume,
            subscriptionConfiguration: new AzureServiceBusSubscriptionConfiguration
            {
                MaxAutoLockRenewalDuration = TimeSpan.FromMilliseconds(renewalMilliseconds)
            });
        var factory = new AzureServiceBusChannelFactory(new AzureServiceBusConsumerFactory(client));
        Message received;

        // Act
        if (useAsync)
        {
            await using var channel = factory.CreateAsyncChannel(subscription);
            var first = await channel.ReceiveAsync(null);
            await channel.AcknowledgeAsync(first);
            await Task.Delay(TimeSpan.FromSeconds(4));
            received = await channel.ReceiveAsync(null);
        }
        else
        {
            using var channel = factory.CreateSyncChannel(subscription);
            var first = channel.Receive(null);
            channel.Acknowledge(first);
            await Task.Delay(TimeSpan.FromSeconds(4));
            received = channel.Receive(null);
        }

        // Assert
        Assert.Equal(MessageType.MT_NONE, received.Header.MessageType);
        Assert.Equal(1, receiver.CompletedCount);
        if (renewalMilliseconds == 0) Assert.Equal(0, receiver.RenewalCount);
        else Assert.True(receiver.RenewalCount > 0);
    }

    [Fact]
    public void When_renewal_duration_is_negative_should_reject_configuration()
    {
        // Arrange
        var receiver = new InMemoryLockingServiceBusReceiver(1, TimeSpan.FromSeconds(2));
        var client = new InMemoryLockingServiceBusClient(receiver);
        var factory = new AzureServiceBusConsumerFactory(client);
        var subscription = new AzureServiceBusSubscription<ASBTestCommand>(
            routingKey: new RoutingKey("locks"),
            subscriptionConfiguration: new AzureServiceBusSubscriptionConfiguration
            {
                MaxAutoLockRenewalDuration = TimeSpan.FromSeconds(-1)
            });

        // Act
        var exception = Assert.Throws<ConfigurationException>(() => factory.Create(subscription));

        // Assert
        Assert.Contains("MaxAutoLockRenewalDuration", exception.Message);
    }

    [Fact]
    public async Task When_renewal_fails_transiently_should_retry_while_the_lock_is_valid()
    {
        // Arrange
        var receiver = new InMemoryLockingServiceBusReceiver(1, TimeSpan.FromSeconds(4))
        {
            RenewalFailuresRemaining = 1,
            RenewalFailureIsTransient = true
        };
        await using var client = new InMemoryLockingServiceBusClient(receiver);
        var subscription = new AzureServiceBusSubscription<ASBTestCommand>(
            channelName: new ChannelName("locks"), routingKey: new RoutingKey("locks"), makeChannels: OnMissingChannel.Assume);
        await using var consumer = new AzureServiceBusConsumerFactory(client).CreateAsync(subscription);

        // Act
        var message = Assert.Single(await consumer.ReceiveAsync());
        await Task.Delay(TimeSpan.FromSeconds(5));
        await consumer.AcknowledgeAsync(message);

        // Assert
        Assert.Equal(1, receiver.CompletedCount);
        Assert.True(receiver.RenewalAttemptCount >= 2);
        Assert.True(receiver.RenewalCount >= 1);
    }

    [Fact]
    public async Task When_a_buffered_message_loses_its_lock_should_skip_dispatch()
    {
        // Arrange
        var receiver = new InMemoryLockingServiceBusReceiver(2, TimeSpan.FromSeconds(2))
        {
            RenewalFailuresRemaining = 1
        };
        await using var client = new InMemoryLockingServiceBusClient(receiver);
        var subscription = new AzureServiceBusSubscription<ASBTestCommand>(
            channelName: new ChannelName("locks"), routingKey: new RoutingKey("locks"),
            bufferSize: 2, makeChannels: OnMissingChannel.Assume);
        var factory = new AzureServiceBusChannelFactory(new AzureServiceBusConsumerFactory(client));
        await using var channel = factory.CreateAsyncChannel(subscription);
        var first = await channel.ReceiveAsync(null);
        await channel.AcknowledgeAsync(first);

        // Act
        await receiver.RenewalStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(TimeSpan.FromMilliseconds(200));
        var received = await channel.ReceiveAsync(null);

        // Assert
        Assert.Equal(MessageType.MT_NONE, received.Header.MessageType);
        Assert.Equal(1, receiver.RenewalAttemptCount);
        Assert.Equal(0, receiver.RenewalCount);
    }
}
