using Paramore.Brighter.MessagingGateway.MQTT;
using Xunit;

namespace Paramore.Brighter.MQTT.Tests.MessagingGateway;

public class When_mqtt_consumer_factory_creates_consumer_should_pass_scheduler
{
    private readonly MqttMessagingGatewayConsumerConfiguration _configuration = new()
    {
        Hostname = "localhost",
        Port = 1883,
        TopicPrefix = "test",
        ClientID = "test-client"
    };

    [Fact]
    public void Should_create_sync_consumer_when_scheduler_provided()
    {
        // Arrange
        var scheduler = new StubMessageScheduler();
        var factory = new MqttMessageConsumerFactory(_configuration, scheduler);

        // Act
        var consumer = factory.Create(new Subscription(
            subscriptionName: new SubscriptionName("test"),
            channelName: new ChannelName("test.queue"),
            routingKey: new RoutingKey("test.key"),
            requestType: typeof(Command),
            messagePumpType: MessagePumpType.Reactor
        ));

        // Assert
        Assert.NotNull(consumer);
        Assert.IsType<MqttMessageConsumer>(consumer);
    }

    [Fact]
    public void Should_create_async_consumer_when_scheduler_provided()
    {
        // Arrange
        var scheduler = new StubMessageScheduler();
        var factory = new MqttMessageConsumerFactory(_configuration, scheduler);

        // Act
        var consumer = factory.CreateAsync(new Subscription(
            subscriptionName: new SubscriptionName("test"),
            channelName: new ChannelName("test.queue"),
            routingKey: new RoutingKey("test.key"),
            requestType: typeof(Command),
            messagePumpType: MessagePumpType.Reactor
        ));

        // Assert
        Assert.NotNull(consumer);
        Assert.IsType<MqttMessageConsumer>(consumer);
    }

    [Fact]
    public void Should_create_consumer_without_scheduler_for_backward_compat()
    {
        // Arrange
        var factory = new MqttMessageConsumerFactory(_configuration);

        // Act
        var consumer = factory.Create(new Subscription(
            subscriptionName: new SubscriptionName("test"),
            channelName: new ChannelName("test.queue"),
            routingKey: new RoutingKey("test.key"),
            requestType: typeof(Command),
            messagePumpType: MessagePumpType.Reactor
        ));

        // Assert
        Assert.NotNull(consumer);
        Assert.IsType<MqttMessageConsumer>(consumer);
    }

    private class StubMessageScheduler : IAmAMessageScheduler;
}
