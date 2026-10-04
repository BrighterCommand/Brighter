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

using Paramore.Brighter.Gcp.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.GcpPubSub;
using Xunit;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway;

/// <summary>
/// Verifies that <see cref="GcpPubSubSubscription"/> implements <see cref="IAmADeliveryCountingSubscription"/>
/// and reports its delivery budget unenforceable when it carries no <see cref="DeadLetterPolicy"/>, because
/// Pub/Sub only populates <c>delivery_attempt</c> for subscriptions with a dead letter policy (R-10, R-11,
/// A-1, ADR 0077).
/// </summary>
public class GcpPubSubSubscriptionDeliveryBudgetUnenforceableTests
{
    [Fact]
    public void When_a_gcp_subscription_has_no_dead_letter_policy_should_report_budget_unenforceable()
    {
        // Arrange & Act
        var subscription = new GcpPubSubSubscription<MyCommand>(
            subscriptionName: new SubscriptionName("test-subscription"),
            channelName: new ChannelName("test-channel"),
            routingKey: new RoutingKey("orders"),
            messagePumpType: MessagePumpType.Reactor);

        // Assert
        Assert.IsAssignableFrom<IAmADeliveryCountingSubscription>(subscription);
        var counting = (IAmADeliveryCountingSubscription)subscription;
        Assert.Equal(subscription.DeadLetter?.MaxDeliveryAttempts, counting.NativeRedriveLimit);
        Assert.Null(counting.NativeRedriveLimit);
        Assert.NotNull(counting.DeliveryBudgetUnenforceableReason);
        Assert.Contains("DeadLetterPolicy", counting.DeliveryBudgetUnenforceableReason);
    }
}
