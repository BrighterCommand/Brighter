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
/// Tests for the R-10 delivery-budget validation rule (ADR 0077): when a subscription's
/// <see cref="Subscription.RequeueCount"/> is at or above its visible native redrive limit,
/// a single Warning is reported naming the subscription, the budget, the native limit, and
/// that the effective limit is the native one.
/// </summary>
public class SubscriptionBudgetAtNativeRedriveLimitValidationTests
{
    [Fact]
    public void When_subscription_budget_meets_native_redrive_limit_should_report_one_warning_naming_budget_and_limit()
    {
        // Arrange — requeueCount: 5, NativeRedriveLimit: 5 — budget equals native limit (R == M)
        var subscription = new DeliveryCountingSubscriptionDouble(
            subscriptionName: new SubscriptionName("native-limit-sub"),
            channelName: new ChannelName("native-limit-channel"),
            routingKey: new RoutingKey("native.limit.routing.key"),
            requeueCount: 5,
            nativeRedriveLimit: 5
        );

        var spec = ConsumerValidationRules.BudgetAtNativeRedriveLimit();

        // Act
        var satisfied = spec.IsSatisfiedBy(subscription);
        var collector = new ValidationResultCollector<Subscription>();
        var results = spec.Accept(collector).ToList();

        // Assert — exactly one Warning naming the budget 5, the native limit 5, and effective limit
        Assert.False(satisfied);
        Assert.Single(results);
        Assert.Equal(ValidationSeverity.Warning, results[0].Error!.Severity);
        Assert.Contains("5", results[0].Error!.Message);
        Assert.Contains("native", results[0].Error!.Message, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void When_subscription_budget_is_below_native_redrive_limit_should_not_report_warning()
    {
        // Arrange — requeueCount: 3 is below NativeRedriveLimit: 5, so Brighter fires first
        var subscription = new DeliveryCountingSubscriptionDouble(
            subscriptionName: new SubscriptionName("under-limit-sub"),
            channelName: new ChannelName("under-limit-channel"),
            routingKey: new RoutingKey("under.limit.routing.key"),
            requeueCount: 3,
            nativeRedriveLimit: 5
        );

        var spec = ConsumerValidationRules.BudgetAtNativeRedriveLimit();

        // Act
        var satisfied = spec.IsSatisfiedBy(subscription);

        // Assert — budget is below native limit; no warning
        Assert.True(satisfied);
    }

    [Fact]
    public void When_subscription_budget_is_minus_one_should_not_report_warning()
    {
        // Arrange — requeueCount: -1 disables the budget; R-10 checks R != -1, so no warning
        var subscription = new DeliveryCountingSubscriptionDouble(
            subscriptionName: new SubscriptionName("unlimited-sub"),
            channelName: new ChannelName("unlimited-channel"),
            routingKey: new RoutingKey("unlimited.routing.key"),
            requeueCount: -1,
            nativeRedriveLimit: 5
        );

        var spec = ConsumerValidationRules.BudgetAtNativeRedriveLimit();

        // Act
        var satisfied = spec.IsSatisfiedBy(subscription);

        // Assert — R == -1 means no budget; the rule does not fire
        Assert.True(satisfied);
    }

    [Fact]
    public void When_subscription_does_not_implement_delivery_counting_interface_should_pass_vacuously()
    {
        // Arrange — a plain Subscription without IAmADeliveryCountingSubscription (R-22)
        var subscription = new Subscription(
            subscriptionName: new SubscriptionName("plain-sub"),
            channelName: new ChannelName("plain-channel"),
            routingKey: new RoutingKey("plain.routing.key"),
            requestType: typeof(MyBareRequest),
            messagePumpType: MessagePumpType.Reactor,
            requeueCount: 5
        );

        var spec = ConsumerValidationRules.BudgetAtNativeRedriveLimit();

        // Act
        var satisfied = spec.IsSatisfiedBy(subscription);

        // Assert — plain Subscription has no NativeRedriveLimit; rule passes vacuously
        Assert.True(satisfied);
    }
}
