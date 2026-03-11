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

using Paramore.Brighter.MessagingGateway.RocketMQ;
using Paramore.Brighter.RocketMQ.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.RocketMQ.Tests.MessagingGateway;

public class RocketSubscriptionDlqRoutingKeyTests
{
    [Fact]
    public void When_creating_rocket_subscription_with_dlq_routing_keys_should_expose_properties()
    {
        // Arrange
        var deadLetterRoutingKey = new RoutingKey("orders-dlq");
        var invalidMessageRoutingKey = new RoutingKey("orders-invalid");

        // Act
        var subscription = new RocketMqSubscription<MyCommand>(
            subscriptionName: new SubscriptionName("test-subscription"),
            channelName: new ChannelName("test-channel"),
            routingKey: new RoutingKey("orders"),
            messagePumpType: MessagePumpType.Reactor,
            deadLetterRoutingKey: deadLetterRoutingKey,
            invalidMessageRoutingKey: invalidMessageRoutingKey
        );

        // Assert
        Assert.IsAssignableFrom<IUseBrighterDeadLetterSupport>(subscription);
        var dlqSupport = (IUseBrighterDeadLetterSupport)subscription;
        Assert.Equal(deadLetterRoutingKey, dlqSupport.DeadLetterRoutingKey);

        Assert.IsAssignableFrom<IUseBrighterInvalidMessageSupport>(subscription);
        var invalidSupport = (IUseBrighterInvalidMessageSupport)subscription;
        Assert.Equal(invalidMessageRoutingKey, invalidSupport.InvalidMessageRoutingKey);
    }

    [Fact]
    public void When_creating_rocket_subscription_without_dlq_routing_keys_should_default_to_null()
    {
        // Arrange & Act
        var subscription = new RocketMqSubscription<MyCommand>(
            subscriptionName: new SubscriptionName("test-subscription"),
            channelName: new ChannelName("test-channel"),
            routingKey: new RoutingKey("orders"),
            messagePumpType: MessagePumpType.Reactor
        );

        // Assert
        Assert.IsAssignableFrom<IUseBrighterDeadLetterSupport>(subscription);
        var dlqSupport = (IUseBrighterDeadLetterSupport)subscription;
        Assert.Null(dlqSupport.DeadLetterRoutingKey);

        Assert.IsAssignableFrom<IUseBrighterInvalidMessageSupport>(subscription);
        var invalidSupport = (IUseBrighterInvalidMessageSupport)subscription;
        Assert.Null(invalidSupport.InvalidMessageRoutingKey);
    }
}
