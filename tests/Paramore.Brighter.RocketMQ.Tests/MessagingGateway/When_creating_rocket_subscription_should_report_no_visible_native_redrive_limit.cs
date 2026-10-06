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

/// <summary>
/// Verifies that <see cref="RocketSubscription"/> (and <see cref="RocketMqSubscription{T}"/>) implement
/// <see cref="IAmADeliveryCountingSubscription"/>, but report no visible native redrive limit: RocketMQ's
/// max retry is configured server-side on the consumer group, not on the subscription, so Brighter cannot
/// see it here (R-10, R-11, ADR 0077 budget-rules table).
/// </summary>
// Broker-free: carries "RocketMQBrokerFree" too (see RocketMqEmptyHeaderPropertyTests for why).
[Trait("Category", "RocketMQ")]
[Trait("Category", "RocketMQBrokerFree")]
public class RocketSubscriptionNativeRedriveLimitTests
{
    [Fact]
    public void When_creating_rocket_subscription_should_report_no_visible_native_redrive_limit()
    {
        // Arrange & Act
        var subscription = new RocketSubscription(
            subscriptionName: new SubscriptionName("test-subscription"),
            channelName: new ChannelName("test-channel"),
            routingKey: new RoutingKey("orders"),
            requestType: typeof(MyCommand),
            messagePumpType: MessagePumpType.Reactor);

        // Assert
        var counting = Assert.IsAssignableFrom<IAmADeliveryCountingSubscription>(subscription);
        Assert.Null(counting.NativeRedriveLimit);
    }

    [Fact]
    public void When_creating_rocket_mq_subscription_of_t_should_report_no_visible_native_redrive_limit()
    {
        // Arrange & Act
        var subscription = new RocketMqSubscription<MyCommand>(
            subscriptionName: new SubscriptionName("test-subscription"),
            channelName: new ChannelName("test-channel"),
            routingKey: new RoutingKey("orders"),
            messagePumpType: MessagePumpType.Reactor);

        // Assert
        var counting = Assert.IsAssignableFrom<IAmADeliveryCountingSubscription>(subscription);
        Assert.Null(counting.NativeRedriveLimit);
    }
}
