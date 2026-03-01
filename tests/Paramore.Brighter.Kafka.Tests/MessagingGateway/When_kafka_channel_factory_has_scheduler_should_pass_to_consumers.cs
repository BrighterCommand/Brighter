using Paramore.Brighter.MessagingGateway.Kafka;
using Xunit;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway;

public class When_kafka_channel_factory_has_scheduler_should_pass_to_consumers
{
    private readonly KafkaMessagingGatewayConfiguration _configuration = new()
    {
        Name = "test",
        BootStrapServers = ["localhost:9092"]
    };

    private readonly KafkaSubscription _subscription = new(
        subscriptionName: new SubscriptionName("test"),
        channelName: new ChannelName("test.queue"),
        routingKey: new RoutingKey("test.key"),
        requestType: typeof(Command),
        messagePumpType: MessagePumpType.Reactor,
        groupId: "test-group",
        makeChannels: OnMissingChannel.Assume
    );

    [Fact]
    public void Should_implement_channel_factory_with_scheduler()
    {
        // Arrange
        var consumerFactory = new KafkaMessageConsumerFactory(_configuration);
        var channelFactory = new ChannelFactory(consumerFactory);

        // Assert
        Assert.IsAssignableFrom<IAmAChannelFactoryWithScheduler>(channelFactory);
    }

    [Fact]
    public void Should_create_sync_channel_when_scheduler_set()
    {
        // Arrange
        var scheduler = new StubMessageScheduler();
        var consumerFactory = new KafkaMessageConsumerFactory(_configuration);
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
        var consumerFactory = new KafkaMessageConsumerFactory(_configuration);
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
        var consumerFactory = new KafkaMessageConsumerFactory(_configuration);
        var channelFactory = new ChannelFactory(consumerFactory);

        // Act
        var channel = channelFactory.CreateSyncChannel(_subscription);

        // Assert
        Assert.NotNull(channel);
        Assert.IsType<Channel>(channel);
    }

    private class StubMessageScheduler : IAmAMessageScheduler;
}
