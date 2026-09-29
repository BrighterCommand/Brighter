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
using Paramore.Brighter.MessagingGateway.AzureServiceBus;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

public class AzureServiceBusChannelFactorySubscriptionTests
{
    private readonly AzureServiceBusChannelFactory _factory;

    public AzureServiceBusChannelFactorySubscriptionTests()
    {
        // Arrange
        _factory = new AzureServiceBusChannelFactory(new AzureServiceBusConsumerFactory(
            new AzureServiceBusConfiguration("Endpoint=sb://localhost/;SharedAccessKeyName=test;SharedAccessKey=dGVzdA==")));
    }

    [Test]
    [Arguments(false, OnMissingChannel.Create)]
    [Arguments(true, OnMissingChannel.Create)]
    [Arguments(false, OnMissingChannel.Validate)]
    [Arguments(true, OnMissingChannel.Validate)]
    [Arguments(false, OnMissingChannel.Assume)]
    [Arguments(true, OnMissingChannel.Assume)]
    public async System.Threading.Tasks.Task When_subscription_does_not_match_should_reject_sync_channel(bool generic, OnMissingChannel makeChannels)
    {
        // Arrange
        var subscription = CreateBaseSubscription(generic, MessagePumpType.Reactor, makeChannels);

        // Act
        var exception = await Assert.That(() => _factory.CreateSyncChannel(subscription)).ThrowsExactly<ConfigurationException>();

        // Assert
        await Assert.That(exception.Message).Contains(nameof(AzureServiceBusSubscription));
    }

    [Test]
    [Arguments(false, OnMissingChannel.Create)]
    [Arguments(true, OnMissingChannel.Create)]
    [Arguments(false, OnMissingChannel.Validate)]
    [Arguments(true, OnMissingChannel.Validate)]
    [Arguments(false, OnMissingChannel.Assume)]
    [Arguments(true, OnMissingChannel.Assume)]
    public async System.Threading.Tasks.Task When_subscription_does_not_match_should_reject_async_channel(bool generic, OnMissingChannel makeChannels)
    {
        // Arrange
        var subscription = CreateBaseSubscription(generic, MessagePumpType.Proactor, makeChannels);

        // Act
        var exception = await Assert.That(() => _factory.CreateAsyncChannel(subscription)).ThrowsExactly<ConfigurationException>();

        // Assert
        await Assert.That(exception.Message).Contains(nameof(AzureServiceBusSubscription));
    }

    [Test]
    [Arguments(false, OnMissingChannel.Create)]
    [Arguments(true, OnMissingChannel.Create)]
    [Arguments(false, OnMissingChannel.Validate)]
    [Arguments(true, OnMissingChannel.Validate)]
    [Arguments(false, OnMissingChannel.Assume)]
    [Arguments(true, OnMissingChannel.Assume)]
    public async Task When_subscription_does_not_match_should_reject_channel_asynchronously(bool generic, OnMissingChannel makeChannels)
    {
        // Arrange
        var subscription = CreateBaseSubscription(generic, MessagePumpType.Proactor, makeChannels);

        // Act
        var exception = await Assert.That(() => _factory.CreateAsyncChannelAsync(subscription)).ThrowsExactly<ConfigurationException>();

        // Assert
        await Assert.That(exception.Message).Contains(nameof(AzureServiceBusSubscription));
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async System.Threading.Tasks.Task When_subscription_matches_should_create_sync_channel(bool generic)
    {
        // Arrange
        var subscription = CreateMatchingSubscription(generic, MessagePumpType.Reactor);

        // Act
        using var channel = _factory.CreateSyncChannel(subscription);

        // Assert
        await Assert.That(channel.Name).IsEqualTo(subscription.ChannelName);
        await Assert.That(channel.RoutingKey).IsEqualTo(subscription.RoutingKey);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async System.Threading.Tasks.Task When_subscription_matches_should_create_async_channel(bool generic)
    {
        // Arrange
        var subscription = CreateMatchingSubscription(generic, MessagePumpType.Proactor);

        // Act
        using var channel = _factory.CreateAsyncChannel(subscription);

        // Assert
        await Assert.That(channel.Name).IsEqualTo(subscription.ChannelName);
        await Assert.That(channel.RoutingKey).IsEqualTo(subscription.RoutingKey);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task When_subscription_matches_should_create_channel_asynchronously(bool generic)
    {
        // Arrange
        var subscription = CreateMatchingSubscription(generic, MessagePumpType.Proactor);

        // Act
        await using var channel = await _factory.CreateAsyncChannelAsync(subscription);

        // Assert
        await Assert.That(channel.Name).IsEqualTo(subscription.ChannelName);
        await Assert.That(channel.RoutingKey).IsEqualTo(subscription.RoutingKey);
    }

    private static Subscription CreateBaseSubscription(bool generic, MessagePumpType messagePumpType, OnMissingChannel makeChannels)
        => generic
            ? new Subscription<Command>(new SubscriptionName("subscription"), new ChannelName("channel"), new RoutingKey("topic"),
                messagePumpType: messagePumpType, makeChannels: makeChannels)
            : new Subscription(new SubscriptionName("subscription"), new ChannelName("channel"), new RoutingKey("topic"),
                messagePumpType: messagePumpType, makeChannels: makeChannels, requestType: typeof(Command));

    private static AzureServiceBusSubscription CreateMatchingSubscription(bool generic, MessagePumpType messagePumpType)
        => generic
            ? new AzureServiceBusSubscription<Command>(new SubscriptionName("subscription"), new ChannelName("channel"), new RoutingKey("topic"),
                messagePumpType: messagePumpType, makeChannels: OnMissingChannel.Assume, timeOut: TimeSpan.FromSeconds(1))
            : new AzureServiceBusSubscription(new SubscriptionName("subscription"), new ChannelName("channel"), new RoutingKey("topic"),
                messagePumpType: messagePumpType, makeChannels: OnMissingChannel.Assume, timeOut: TimeSpan.FromSeconds(1), requestType: typeof(Command));
}
