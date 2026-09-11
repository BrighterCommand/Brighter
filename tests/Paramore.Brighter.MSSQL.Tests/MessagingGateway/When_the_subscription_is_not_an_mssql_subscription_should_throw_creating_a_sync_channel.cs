#region Licence
/* The MIT License (MIT)

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
using Paramore.Brighter.MessagingGateway.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.MessagingGateway;

/// <summary>
/// The MSSQL <see cref="ChannelFactory"/> downcasts the subscription it is handed, so a plain
/// <c>Subscription&lt;T&gt;</c> compiles, configures, and then fails as the Dispatcher builds its
/// channels. Nothing in the type system says so; these tests are what says so.
/// </summary>
/// <remarks>
/// No database is needed. The factory throws before it constructs a consumer, and the positive
/// control gets a channel because <c>MsSqlMessageConsumer</c> opens no connection in its
/// constructor — so this sits beside the other connection-free tests in this folder.
/// </remarks>
[Trait("Category", "MSSQL")]
public class MsSqlChannelFactorySubscriptionTypeTests
{
    private readonly RelationalDatabaseConfiguration _configuration =
        new("Server=localhost;Database=test;Trusted_Connection=True;");

    private static Subscription<MyEvent> PlainSubscription() =>
        new(new SubscriptionName("plain"),
            new ChannelName("test.topic"),
            new RoutingKey("test.topic"));

    private static MsSqlSubscription<MyEvent> AnMsSqlSubscription() =>
        new(new SubscriptionName("mssql"),
            new ChannelName("test.topic"),
            new RoutingKey("test.topic"));

    private ChannelFactory CreateChannelFactory() =>
        new(new MsSqlMessageConsumerFactory(_configuration));

    [Fact]
    public void When_the_subscription_is_not_an_mssql_subscription_should_throw_creating_a_sync_channel()
    {
        // Arrange
        var channelFactory = CreateChannelFactory();

        // Act
        var exception = Assert.Throws<ConfigurationException>(
            () => channelFactory.CreateSyncChannel(PlainSubscription()));

        // Assert
        Assert.Contains("MsSqlSubscription", exception.Message);
    }

    [Fact]
    public void When_the_subscription_is_not_an_mssql_subscription_should_throw_creating_an_async_channel()
    {
        // Arrange
        var channelFactory = CreateChannelFactory();

        // Act
        var exception = Assert.Throws<ConfigurationException>(
            () => channelFactory.CreateAsyncChannel(PlainSubscription()));

        // Assert
        Assert.Contains("MsSqlSubscription", exception.Message);
    }

    [Fact]
    public async Task When_the_subscription_is_not_an_mssql_subscription_should_throw_creating_an_async_channel_asynchronously()
    {
        // Arrange
        var channelFactory = CreateChannelFactory();

        // Act
        var exception = await Assert.ThrowsAsync<ConfigurationException>(
            () => channelFactory.CreateAsyncChannelAsync(PlainSubscription()));

        // Assert
        Assert.Contains("MsSqlSubscription", exception.Message);
    }

    // The controls. Without one per creation method, any of the three could regress to an
    // unconditional throw and every test above would stay green.
    [Fact]
    public void When_the_subscription_is_an_mssql_subscription_should_create_a_sync_channel()
    {
        // Arrange
        var channelFactory = CreateChannelFactory();

        // Act
        using var channel = channelFactory.CreateSyncChannel(AnMsSqlSubscription());

        // Assert
        Assert.NotNull(channel);
        Assert.Equal(new ChannelName("test.topic"), channel.Name);
    }

    [Fact]
    public void When_the_subscription_is_an_mssql_subscription_should_create_an_async_channel()
    {
        // Arrange
        var channelFactory = CreateChannelFactory();

        // Act
        using var channel = channelFactory.CreateAsyncChannel(AnMsSqlSubscription());

        // Assert
        Assert.NotNull(channel);
        Assert.Equal(new ChannelName("test.topic"), channel.Name);
    }

    [Fact]
    public async Task When_the_subscription_is_an_mssql_subscription_should_create_an_async_channel_asynchronously()
    {
        // Arrange
        var channelFactory = CreateChannelFactory();

        // Act
        using var channel = await channelFactory.CreateAsyncChannelAsync(AnMsSqlSubscription());

        // Assert
        Assert.NotNull(channel);
        Assert.Equal(new ChannelName("test.topic"), channel.Name);
    }

    // The error message offers "MsSqlSubscription or MsSqlSubscription<T>", so the non-generic
    // form has to be accepted for the message to be true.
    [Fact]
    public void When_the_subscription_is_a_non_generic_mssql_subscription_should_create_a_channel()
    {
        // Arrange
        var channelFactory = CreateChannelFactory();
        var subscription = new MsSqlSubscription(
            new SubscriptionName("non-generic"),
            new ChannelName("test.topic"),
            new RoutingKey("test.topic"),
            typeof(MyEvent));

        // Act
        using var channel = channelFactory.CreateSyncChannel(subscription);

        // Assert
        Assert.NotNull(channel);
        Assert.Equal(new ChannelName("test.topic"), channel.Name);
    }

    private class MyEvent : Event
    {
        public MyEvent() : base(Guid.NewGuid().ToString()) { }
    }
}
