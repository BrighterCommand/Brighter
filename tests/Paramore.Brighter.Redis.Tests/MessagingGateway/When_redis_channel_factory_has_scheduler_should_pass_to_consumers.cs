using Paramore.Brighter.MessagingGateway.Redis;

namespace Paramore.Brighter.Redis.Tests.MessagingGateway;

public class When_redis_channel_factory_has_scheduler_should_pass_to_consumers
{
    private readonly RedisMessagingGatewayConfiguration _configuration = new()
    {
        RedisConnectionString = "localhost:6379",
        MaxPoolSize = 10
    };

    private readonly RedisSubscription _subscription = new(
        subscriptionName: new SubscriptionName("test"),
        channelName: new ChannelName("test.queue"),
        routingKey: new RoutingKey("test.key"),
        requestType: typeof(Command),
        messagePumpType: MessagePumpType.Reactor
    );

    [Test]
    public async Task Should_implement_channel_factory_with_scheduler()
    {
        // Arrange
        var consumerFactory = new RedisMessageConsumerFactory(_configuration);
        var channelFactory = new ChannelFactory(consumerFactory);

        // Assert
        await Assert.That(channelFactory).IsAssignableTo<IAmAChannelFactoryWithScheduler>();
    }

    [Test]
    public async Task Should_create_sync_channel_when_scheduler_set()
    {
        // Arrange
        var scheduler = new StubMessageScheduler();
        var consumerFactory = new RedisMessageConsumerFactory(_configuration);
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
        var consumerFactory = new RedisMessageConsumerFactory(_configuration);
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
        var consumerFactory = new RedisMessageConsumerFactory(_configuration);
        var channelFactory = new ChannelFactory(consumerFactory);

        // Act
        var channel = channelFactory.CreateSyncChannel(_subscription);

        // Assert
        await Assert.That(channel).IsNotNull();
        await Assert.That(channel).IsTypeOf<Channel>();
    }

    private class StubMessageScheduler : IAmAMessageScheduler;
}
