using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.MQTT;
using Xunit;

namespace Paramore.Brighter.MQTT.Tests.MessagingGateway;

public class When_mqtt_channel_factory_creates_channel_should_use_consumer_factory
{
    private readonly MqttMessageConsumerFactory _consumerFactory;
    private readonly ChannelFactory _channelFactory;

    private readonly Subscription _subscription = new(
        subscriptionName: new SubscriptionName("test"),
        channelName: new ChannelName("test.queue"),
        routingKey: new RoutingKey("test.key"),
        requestType: typeof(Command),
        messagePumpType: MessagePumpType.Reactor
    );

    public When_mqtt_channel_factory_creates_channel_should_use_consumer_factory()
    {
        var configuration = new MqttMessagingGatewayConsumerConfiguration
        {
            Hostname = "localhost",
            Port = 1883,
            TopicPrefix = "test",
            ClientID = "test-client"
        };

        _consumerFactory = new MqttMessageConsumerFactory(configuration);
        _channelFactory = new ChannelFactory(_consumerFactory);
    }

    [Fact]
    public void Should_create_sync_channel()
    {
        // Act
        var channel = _channelFactory.CreateSyncChannel(_subscription);

        // Assert
        Assert.NotNull(channel);
        Assert.IsType<Channel>(channel);
    }

    [Fact]
    public void Should_create_async_channel()
    {
        // Act
        var channel = _channelFactory.CreateAsyncChannel(_subscription);

        // Assert
        Assert.NotNull(channel);
        Assert.IsType<ChannelAsync>(channel);
    }

    [Fact]
    public async Task Should_create_async_channel_async()
    {
        // Act
        var channel = await _channelFactory.CreateAsyncChannelAsync(_subscription);

        // Assert
        Assert.NotNull(channel);
        Assert.IsType<ChannelAsync>(channel);
    }

    [Fact]
    public void Should_implement_channel_factory_with_scheduler()
    {
        // Assert
        Assert.IsAssignableFrom<IAmAChannelFactoryWithScheduler>(_channelFactory);
    }

    [Fact]
    public void Should_accept_scheduler_property()
    {
        // Arrange
        var scheduler = new StubMessageScheduler();

        // Act
        ((IAmAChannelFactoryWithScheduler)_channelFactory).Scheduler = scheduler;

        // Assert
        Assert.Equal(scheduler, ((IAmAChannelFactoryWithScheduler)_channelFactory).Scheduler);
    }

    private class StubMessageScheduler : IAmAMessageScheduler;
}
