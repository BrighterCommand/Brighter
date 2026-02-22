using Paramore.Brighter.MessagingGateway.Redis;
using Xunit;

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

    [Fact]
    public void Should_implement_channel_factory_with_scheduler()
    {
        // Arrange
        var consumerFactory = new RedisMessageConsumerFactory(_configuration);
        var channelFactory = new ChannelFactory(consumerFactory);

        // Assert
        Assert.IsAssignableFrom<IAmAChannelFactoryWithScheduler>(channelFactory);
    }

    [Fact]
    public void Should_create_sync_channel_when_scheduler_set()
    {
        // Arrange
        var scheduler = new StubMessageScheduler();
        var consumerFactory = new RedisMessageConsumerFactory(_configuration);
        var channelFactory = new ChannelFactory(consumerFactory);
        ((IAmAChannelFactoryWithScheduler)channelFactory).Scheduler = scheduler;

        // Act
        var channel = channelFactory.CreateSyncChannel(_subscription);

        // Assert
        Assert.NotNull(channel);
        Assert.IsType<Channel>(channel);
    }

    [Fact]
    public void Should_create_async_channel_when_scheduler_set()
    {
        // Arrange
        var scheduler = new StubMessageScheduler();
        var consumerFactory = new RedisMessageConsumerFactory(_configuration);
        var channelFactory = new ChannelFactory(consumerFactory);
        ((IAmAChannelFactoryWithScheduler)channelFactory).Scheduler = scheduler;

        // Act
        var channel = channelFactory.CreateAsyncChannel(_subscription);

        // Assert
        Assert.NotNull(channel);
        Assert.IsType<ChannelAsync>(channel);
    }

    [Fact]
    public void Should_create_channel_without_scheduler_for_backward_compat()
    {
        // Arrange — no scheduler set
        var consumerFactory = new RedisMessageConsumerFactory(_configuration);
        var channelFactory = new ChannelFactory(consumerFactory);

        // Act
        var channel = channelFactory.CreateSyncChannel(_subscription);

        // Assert
        Assert.NotNull(channel);
        Assert.IsType<Channel>(channel);
    }

    private class StubMessageScheduler : IAmAMessageScheduler;
}
