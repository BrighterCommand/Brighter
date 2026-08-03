#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using Paramore.Brighter.MessagingGateway.RocketMQ;
using Paramore.Brighter.RocketMQ.Tests.TestDoubles;
using Paramore.Brighter.RocketMQ.Tests.Utils;
using Xunit;

namespace Paramore.Brighter.RocketMQ.Tests.MessagingGateway;

[Trait("Category", "RocketMQ")]
public class RocketConsumerFactoryDlqTests : IDisposable
{
    private readonly RocketMessageConsumerFactory _factory;
    private readonly RocketMessagingGatewayConnection _connection;
    private IAmAMessageConsumerSync? _consumer;

    public RocketConsumerFactoryDlqTests()
    {
        _connection = GatewayFactory.CreateConnection();
        _factory = new RocketMessageConsumerFactory(_connection);
    }

    [Fact]
    public void When_creating_rocket_consumer_with_dlq_subscription_should_pass_routing_keys()
    {
        // Arrange
        var dlqRoutingKey = new RoutingKey("orders-dlq");
        var invalidRoutingKey = new RoutingKey("orders-invalid");

        var subscription = new RocketMqSubscription<MyCommand>(
            subscriptionName: new SubscriptionName("test-subscription"),
            channelName: new ChannelName("test-channel"),
            routingKey: new RoutingKey("orders"),
            consumerGroup: Guid.NewGuid().ToString(),
            messagePumpType: MessagePumpType.Reactor,
            deadLetterRoutingKey: dlqRoutingKey,
            invalidMessageRoutingKey: invalidRoutingKey
        );

        // Act
        _consumer = _factory.Create(subscription);

        // Assert - verify the factory passed routing keys to the consumer
        var consumer = Assert.IsType<RocketMessageConsumer>(_consumer);

        Assert.NotNull(consumer.DeadLetterRoutingKey);
        Assert.Equal("orders-dlq", consumer.DeadLetterRoutingKey.Value);

        Assert.NotNull(consumer.InvalidMessageRoutingKey);
        Assert.Equal("orders-invalid", consumer.InvalidMessageRoutingKey.Value);
    }

    public void Dispose()
    {
        _consumer?.Dispose();
    }
}
