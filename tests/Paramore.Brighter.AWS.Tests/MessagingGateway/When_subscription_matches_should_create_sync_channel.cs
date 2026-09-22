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

using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Xunit;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway;

public class AwsChannelFactorySubscriptionTests
{
    private readonly ChannelFactory _factory;

    public AwsChannelFactorySubscriptionTests()
    {
        // Arrange
        _factory = new ChannelFactory(new AWSMessagingGatewayConnection(
            new BasicAWSCredentials("test", "test"), RegionEndpoint.EUWest1));
    }

    [Theory]
    [InlineData(false, OnMissingChannel.Create)]
    [InlineData(true, OnMissingChannel.Create)]
    [InlineData(false, OnMissingChannel.Validate)]
    [InlineData(true, OnMissingChannel.Validate)]
    [InlineData(false, OnMissingChannel.Assume)]
    [InlineData(true, OnMissingChannel.Assume)]
    public void When_subscription_does_not_match_should_reject_sync_channel(bool generic, OnMissingChannel makeChannels)
    {
        // Arrange
        var subscription = CreateBaseSubscription(generic, MessagePumpType.Reactor, makeChannels);

        // Act
        var exception = Assert.Throws<ConfigurationException>(() => _factory.CreateSyncChannel(subscription));

        // Assert
        Assert.Contains(nameof(SqsSubscription), exception.Message);
    }

    [Theory]
    [InlineData(false, OnMissingChannel.Create)]
    [InlineData(true, OnMissingChannel.Create)]
    [InlineData(false, OnMissingChannel.Validate)]
    [InlineData(true, OnMissingChannel.Validate)]
    [InlineData(false, OnMissingChannel.Assume)]
    [InlineData(true, OnMissingChannel.Assume)]
    public void When_subscription_does_not_match_should_reject_async_channel(bool generic, OnMissingChannel makeChannels)
    {
        // Arrange
        var subscription = CreateBaseSubscription(generic, MessagePumpType.Proactor, makeChannels);

        // Act
        var exception = Assert.Throws<ConfigurationException>(() => _factory.CreateAsyncChannel(subscription));

        // Assert
        Assert.Contains(nameof(SqsSubscription), exception.Message);
    }

    [Theory]
    [InlineData(false, OnMissingChannel.Create)]
    [InlineData(true, OnMissingChannel.Create)]
    [InlineData(false, OnMissingChannel.Validate)]
    [InlineData(true, OnMissingChannel.Validate)]
    [InlineData(false, OnMissingChannel.Assume)]
    [InlineData(true, OnMissingChannel.Assume)]
    public async Task When_subscription_does_not_match_should_reject_channel_asynchronously(bool generic, OnMissingChannel makeChannels)
    {
        // Arrange
        var subscription = CreateBaseSubscription(generic, MessagePumpType.Proactor, makeChannels);

        // Act
        var exception = await Assert.ThrowsAsync<ConfigurationException>(() => _factory.CreateAsyncChannelAsync(subscription));

        // Assert
        Assert.Contains(nameof(SqsSubscription), exception.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void When_subscription_matches_should_create_sync_channel(bool generic)
    {
        // Arrange
        var subscription = CreateMatchingSubscription(generic, MessagePumpType.Reactor);

        // Act
        using var channel = _factory.CreateSyncChannel(subscription);

        // Assert
        Assert.Equal(subscription.ChannelName, channel.Name);
        Assert.Equal(subscription.RoutingKey, channel.RoutingKey);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void When_subscription_matches_should_create_async_channel(bool generic)
    {
        // Arrange
        var subscription = CreateMatchingSubscription(generic, MessagePumpType.Proactor);

        // Act
        using var channel = _factory.CreateAsyncChannel(subscription);

        // Assert
        Assert.Equal(subscription.ChannelName, channel.Name);
        Assert.Equal(subscription.RoutingKey, channel.RoutingKey);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_subscription_matches_should_create_channel_asynchronously(bool generic)
    {
        // Arrange
        var subscription = CreateMatchingSubscription(generic, MessagePumpType.Proactor);

        // Act
        await using var channel = await _factory.CreateAsyncChannelAsync(subscription);

        // Assert
        Assert.Equal(subscription.ChannelName, channel.Name);
        Assert.Equal(subscription.RoutingKey, channel.RoutingKey);
    }

    private static Subscription CreateBaseSubscription(bool generic, MessagePumpType messagePumpType, OnMissingChannel makeChannels)
        => generic
            ? new Subscription<Command>(new SubscriptionName("subscription"), new ChannelName("channel"), new RoutingKey("topic"),
                messagePumpType: messagePumpType, makeChannels: makeChannels)
            : new Subscription(new SubscriptionName("subscription"), new ChannelName("channel"), new RoutingKey("topic"),
                messagePumpType: messagePumpType, makeChannels: makeChannels, requestType: typeof(Command));

    private static SqsSubscription CreateMatchingSubscription(bool generic, MessagePumpType messagePumpType)
        => generic
            ? new SqsSubscription<Command>(new SubscriptionName("subscription"), new ChannelName("channel"), ChannelType.PointToPoint, new RoutingKey("topic"),
                messagePumpType: messagePumpType, makeChannels: OnMissingChannel.Assume)
            : new SqsSubscription(new SubscriptionName("subscription"), new ChannelName("channel"), ChannelType.PointToPoint, new RoutingKey("topic"),
                messagePumpType: messagePumpType, makeChannels: OnMissingChannel.Assume, requestType: typeof(Command));
}
