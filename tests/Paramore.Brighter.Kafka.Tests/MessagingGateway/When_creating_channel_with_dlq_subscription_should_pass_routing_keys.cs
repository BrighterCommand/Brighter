#region Licence
/* The MIT License (MIT)
Copyright © 2025 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Reflection;
using Paramore.Brighter.Kafka.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.Kafka;
using Xunit;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway;

public class KafkaMessageConsumerFactoryDLQTests : IDisposable
{
    private readonly KafkaMessageConsumerFactory _factory;
    private IAmAMessageConsumerSync? _consumer;

    public KafkaMessageConsumerFactoryDLQTests()
    {
        //Arrange
        _factory = new KafkaMessageConsumerFactory(
            new KafkaMessagingGatewayConfiguration
            {
                Name = "Kafka Consumer Factory DLQ Test",
                BootStrapServers = new[] { "localhost:9092" }
            });
    }

    [Fact]
    public void When_creating_channel_with_dlq_subscription_should_pass_routing_keys()
    {
        //Arrange
        var topic = Guid.NewGuid().ToString();
        var dlqTopic = $"{topic}.dlq";
        var invalidTopic = $"{topic}.invalid";

        var subscription = new KafkaSubscription<MyCommand>(
            subscriptionName: new SubscriptionName("Test Subscription"),
            channelName: new ChannelName("test-channel"),
            routingKey: new RoutingKey(topic),
            groupId: "test-group",
            numOfPartitions: 1,
            replicationFactor: 1,
            messagePumpType: MessagePumpType.Reactor,
            deadLetterRoutingKey: new RoutingKey(dlqTopic),
            invalidMessageRoutingKey: new RoutingKey(invalidTopic)
        );

        //Act
        _consumer = _factory.Create(subscription);

        //Assert - verify the factory passed routing keys to the consumer
        Assert.NotNull(_consumer);

        // Use reflection to verify the private fields were set correctly
        var consumerType = _consumer.GetType();
        var dlqRoutingKeyField = consumerType.GetField("_deadLetterRoutingKey",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var invalidRoutingKeyField = consumerType.GetField("_invalidMessageRoutingKey",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(dlqRoutingKeyField);
        Assert.NotNull(invalidRoutingKeyField);

        var actualDlqRoutingKey = dlqRoutingKeyField.GetValue(_consumer) as RoutingKey;
        var actualInvalidRoutingKey = invalidRoutingKeyField.GetValue(_consumer) as RoutingKey;

        Assert.NotNull(actualDlqRoutingKey);
        Assert.Equal(dlqTopic, actualDlqRoutingKey.Value);

        Assert.NotNull(actualInvalidRoutingKey);
        Assert.Equal(invalidTopic, actualInvalidRoutingKey.Value);
    }

    public void Dispose()
    {
        _consumer?.Dispose();
    }
}
