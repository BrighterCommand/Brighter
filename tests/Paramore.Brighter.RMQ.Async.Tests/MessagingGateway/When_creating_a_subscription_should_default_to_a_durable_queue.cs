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

using Paramore.Brighter.MessagingGateway.RMQ.Async;
using Paramore.Brighter.RMQ.Async.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.RMQ.Async.Tests.MessagingGateway;

/// <summary>
/// RabbitMQ 4.3 deprecates transient non-exclusive queues and refuses to declare them by default
/// ("Feature `transient_nonexcl_queues` is deprecated"). This gateway targets RabbitMQ 4.x, so a
/// subscription that does not say otherwise must ask for a durable queue, or the broker rejects
/// the declaration and the consumer cannot connect at all.
/// </summary>
[Trait("Category", "RMQ")]
public class RmqSubscriptionDurabilityDefaultTests
{
    [Fact]
    public void When_creating_a_subscription_should_default_to_a_durable_queue()
    {
        // Arrange, Act - a subscription that says nothing about durability
        var subscription = new RmqSubscription(
            new SubscriptionName("Test Subscription"),
            new ChannelName("test.queue"),
            new RoutingKey("test.topic"),
            typeof(MyCommand),
            messagePumpType: MessagePumpType.Proactor);

        // Assert
        Assert.True(subscription.IsDurable);
    }

    [Fact]
    public void When_creating_a_typed_subscription_should_default_to_a_durable_queue()
    {
        // Arrange, Act - the generic overload forwards its own default to the base class, so it
        // has to be kept in step; a divergence here is invisible until the broker rejects a declare
        var subscription = new RmqSubscription<MyCommand>(
            new SubscriptionName("Test Subscription"),
            new ChannelName("test.queue"),
            new RoutingKey("test.topic"));

        // Assert
        Assert.True(subscription.IsDurable);
    }

    [Fact]
    public void When_creating_a_subscription_that_asks_for_a_transient_queue_should_not_be_durable()
    {
        // Arrange, Act - the default moved, but opting out must still be possible: RabbitMQ 4.3 can
        // be configured to permit the deprecated feature, and 3.x permits it outright
        var subscription = new RmqSubscription(
            new SubscriptionName("Test Subscription"),
            new ChannelName("test.queue"),
            new RoutingKey("test.topic"),
            typeof(MyCommand),
            isDurable: false,
            messagePumpType: MessagePumpType.Proactor);

        // Assert
        Assert.False(subscription.IsDurable);
    }
}
