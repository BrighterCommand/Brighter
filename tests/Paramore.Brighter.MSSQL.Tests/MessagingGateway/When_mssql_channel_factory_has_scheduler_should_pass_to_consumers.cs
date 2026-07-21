using Paramore.Brighter.MessagingGateway.MsSql;
using Paramore.Brighter.MsSql;

namespace Paramore.Brighter.MSSQL.Tests.MessagingGateway;

public class When_mssql_channel_factory_has_scheduler_should_pass_to_consumers
{
    private readonly RelationalDatabaseConfiguration _configuration = new(
        "Server=localhost;Database=test;Trusted_Connection=True;",
        queueStoreTable: "BrighterMessages"
    );

    private readonly MsSqlSubscription _subscription = new(
        subscriptionName: new SubscriptionName("test"),
        channelName: new ChannelName("test.queue"),
        routingKey: new RoutingKey("test.key"),
        requestType: typeof(Command),
        messagePumpType: MessagePumpType.Reactor,
        makeChannels: OnMissingChannel.Assume
    );

    [Test]
    public async Task Should_implement_channel_factory_with_scheduler()
    {
        // Arrange
        var consumerFactory = new MsSqlMessageConsumerFactory(_configuration);
        var channelFactory = new ChannelFactory(consumerFactory);

        // Assert
        await Assert.That(channelFactory).IsAssignableTo<IAmAChannelFactoryWithScheduler>();
    }

    [Test]
    public async Task Should_create_sync_channel_when_scheduler_set()
    {
        // Arrange
        var scheduler = new StubMessageScheduler();
        var consumerFactory = new MsSqlMessageConsumerFactory(_configuration);
        var channelFactory = new ChannelFactory(consumerFactory);
        ((IAmAChannelFactoryWithScheduler)channelFactory).Scheduler = scheduler;

        // Act
        var channel = channelFactory.CreateSyncChannel(_subscription);

        // Assert
        await Assert.That(channel).IsNotNull();
        await Assert.That(channel).IsTypeOf<Channel>();
    }

    [Test]
    public async Task Should_create_async_channel_when_scheduler_set()
    {
        // Arrange
        var scheduler = new StubMessageScheduler();
        var consumerFactory = new MsSqlMessageConsumerFactory(_configuration);
        var channelFactory = new ChannelFactory(consumerFactory);
        ((IAmAChannelFactoryWithScheduler)channelFactory).Scheduler = scheduler;

        // Act
        var channel = await channelFactory.CreateAsyncChannelAsync(_subscription);

        // Assert
        await Assert.That(channel).IsNotNull();
        await Assert.That(channel).IsTypeOf<ChannelAsync>();
    }

    [Test]
    public async Task Should_create_channel_without_scheduler_for_backward_compat()
    {
        // Arrange — no scheduler set
        var consumerFactory = new MsSqlMessageConsumerFactory(_configuration);
        var channelFactory = new ChannelFactory(consumerFactory);

        // Act
        var channel = channelFactory.CreateSyncChannel(_subscription);

        // Assert
        await Assert.That(channel).IsNotNull();
        await Assert.That(channel).IsTypeOf<Channel>();
    }

    private class StubMessageScheduler : IAmAMessageScheduler;
}
