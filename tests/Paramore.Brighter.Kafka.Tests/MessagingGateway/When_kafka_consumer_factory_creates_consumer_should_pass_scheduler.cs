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

using Paramore.Brighter.MessagingGateway.Kafka;
using Xunit;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway;

public class When_kafka_consumer_factory_creates_consumer_should_pass_scheduler
{
    private readonly KafkaMessagingGatewayConfiguration _configuration = new()
    {
        Name = "test",
        BootStrapServers = ["localhost:9092"]
    };

    private readonly KafkaSubscription _subscription = new(
        subscriptionName: new SubscriptionName("test"),
        channelName: new ChannelName("test.topic"),
        routingKey: new RoutingKey("test.topic"),
        requestType: typeof(Command),
        groupId: "test-group",
        messagePumpType: MessagePumpType.Reactor,
        makeChannels: OnMissingChannel.Assume
    );

    [Fact]
    public void Should_create_consumer_when_scheduler_provided()
    {
        // Arrange — factory constructed with a scheduler
        var scheduler = new StubMessageScheduler();
        var factory = new KafkaMessageConsumerFactory(_configuration, scheduler);

        // Act
        var consumer = factory.Create(_subscription);

        // Assert — consumer is created successfully
        Assert.NotNull(consumer);
        Assert.IsType<KafkaMessageConsumer>(consumer);
    }

    [Fact]
    public void Should_create_async_consumer_when_scheduler_provided()
    {
        // Arrange — factory constructed with a scheduler
        var scheduler = new StubMessageScheduler();
        var factory = new KafkaMessageConsumerFactory(_configuration, scheduler);

        // Act
        var consumer = factory.CreateAsync(_subscription);

        // Assert — consumer is created successfully
        Assert.NotNull(consumer);
        Assert.IsType<KafkaMessageConsumer>(consumer);
    }

    [Fact]
    public void Should_create_consumer_without_scheduler_for_backward_compat()
    {
        // Arrange — factory constructed without a scheduler (backward compat)
        var factory = new KafkaMessageConsumerFactory(_configuration);

        // Act
        var consumer = factory.Create(_subscription);

        // Assert — consumer is created successfully without scheduler
        Assert.NotNull(consumer);
        Assert.IsType<KafkaMessageConsumer>(consumer);
    }

    private class StubMessageScheduler : IAmAMessageScheduler;
}
