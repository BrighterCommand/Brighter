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

using System.Linq;
using Paramore.Brighter.Core.Tests.Validation.TestDoubles;
using Paramore.Brighter.ServiceActivator.Validation;
using Paramore.Brighter.Validation;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Validation;

/// <summary>
/// Tests for the R-11 delivery-budget validation rule (ADR 0077): when a subscription's
/// delivery count cannot advance, a single Warning is reported naming the subscription,
/// the configured budget, and the reason the count cannot advance.
/// </summary>
public class SubscriptionUnenforceableDeliveryCountValidationTests
{
    [Fact]
    public void When_subscription_cannot_advance_delivery_count_should_report_one_warning_naming_budget_and_reason()
    {
        // Arrange — requeueCount: 3, unenforceability reason "no DeadLetterPolicy"
        var subscription = new UnenforceableDeliveryCountingSubscriptionDouble(
            subscriptionName: new SubscriptionName("unenforceable-sub"),
            channelName: new ChannelName("unenforceable-channel"),
            routingKey: new RoutingKey("unenforceable.routing.key"),
            requeueCount: 3,
            deliveryBudgetUnenforceableReason: "no DeadLetterPolicy"
        );

        var spec = ConsumerValidationRules.UnenforceableBudget();

        // Act
        var satisfied = spec.IsSatisfiedBy(subscription);
        var collector = new ValidationResultCollector<Subscription>();
        var results = spec.Accept(collector).ToList();

        // Assert — exactly one Warning naming the subscription, 3, and "no DeadLetterPolicy"
        Assert.False(satisfied);
        Assert.Single(results);
        Assert.Equal(ValidationSeverity.Warning, results[0].Error!.Severity);
        Assert.Contains("3", results[0].Error!.Message);
        Assert.Contains("no DeadLetterPolicy", results[0].Error!.Message);
    }

    [Fact]
    public void When_subscription_unenforceability_reason_is_null_should_not_report_warning()
    {
        // Arrange — null reason means the budget is enforceable; rule does not fire
        var subscription = new UnenforceableDeliveryCountingSubscriptionDouble(
            subscriptionName: new SubscriptionName("enforceable-sub"),
            channelName: new ChannelName("enforceable-channel"),
            routingKey: new RoutingKey("enforceable.routing.key"),
            requeueCount: 3,
            deliveryBudgetUnenforceableReason: null
        );

        var spec = ConsumerValidationRules.UnenforceableBudget();

        // Act
        var satisfied = spec.IsSatisfiedBy(subscription);

        // Assert — null reason; rule does not fire
        Assert.True(satisfied);
    }

    [Fact]
    public void When_subscription_budget_is_minus_one_with_unenforceable_reason_should_not_report_warning()
    {
        // Arrange — requeueCount: -1 disables the budget (R-6); R-11 checks R != -1
        var subscription = new UnenforceableDeliveryCountingSubscriptionDouble(
            subscriptionName: new SubscriptionName("unlimited-unenforceable-sub"),
            channelName: new ChannelName("unlimited-unenforceable-channel"),
            routingKey: new RoutingKey("unlimited.unenforceable.routing.key"),
            requeueCount: -1,
            deliveryBudgetUnenforceableReason: "no DeadLetterPolicy"
        );

        var spec = ConsumerValidationRules.UnenforceableBudget();

        // Act
        var satisfied = spec.IsSatisfiedBy(subscription);

        // Assert — R == -1 means no budget; R-11 does not fire
        Assert.True(satisfied);
    }
}
