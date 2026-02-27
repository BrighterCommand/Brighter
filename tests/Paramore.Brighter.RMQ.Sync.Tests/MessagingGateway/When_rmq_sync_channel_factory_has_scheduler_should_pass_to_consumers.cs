using System;
using Paramore.Brighter.MessagingGateway.RMQ.Sync;
using Xunit;

namespace Paramore.Brighter.RMQ.Sync.Tests.MessagingGateway;

public class When_rmq_sync_channel_factory_has_scheduler_should_pass_to_consumers
{
    private readonly RmqMessagingGatewayConnection _connection = new()
    {
        AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672")),
        Exchange = new Exchange("test.exchange")
    };

    private readonly RmqSubscription _subscription = new(
        subscriptionName: new SubscriptionName("test"),
        channelName: new ChannelName("test.queue"),
        routingKey: new RoutingKey("test.key"),
        requestType: typeof(Command),
        messagePumpType: MessagePumpType.Reactor,
        makeChannels: OnMissingChannel.Assume
    );

    [Fact]
    public void Should_implement_channel_factory_with_scheduler()
    {
        // Arrange
        var consumerFactory = new RmqMessageConsumerFactory(_connection);
        var channelFactory = new ChannelFactory(consumerFactory);

        // Assert
        Assert.IsAssignableFrom<IAmAChannelFactoryWithScheduler>(channelFactory);
    }

    [Fact]
    public void Should_create_sync_channel_when_scheduler_set()
    {
        // Arrange
        var scheduler = new StubMessageScheduler();
        var consumerFactory = new RmqMessageConsumerFactory(_connection);
        var channelFactory = new ChannelFactory(consumerFactory);
        ((IAmAChannelFactoryWithScheduler)channelFactory).Scheduler = scheduler;

        // Act
        var channel = channelFactory.CreateSyncChannel(_subscription);

        // Assert
        Assert.NotNull(channel);
        Assert.IsType<Channel>(channel);
    }

    [Fact]
    public void Should_create_channel_without_scheduler_for_backward_compat()
    {
        // Arrange — no scheduler set
        var consumerFactory = new RmqMessageConsumerFactory(_connection);
        var channelFactory = new ChannelFactory(consumerFactory);

        // Act
        var channel = channelFactory.CreateSyncChannel(_subscription);

        // Assert
        Assert.NotNull(channel);
        Assert.IsType<Channel>(channel);
    }

    private class StubMessageScheduler : IAmAMessageScheduler;
}
