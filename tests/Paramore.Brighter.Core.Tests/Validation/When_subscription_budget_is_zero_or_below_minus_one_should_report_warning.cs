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
/// Tests for the R-7 delivery-budget validation rule (ADR 0077): a subscription whose
/// <see cref="Subscription.RequeueCount"/> is 0 or below -1 reports a single Warning naming
/// the subscription, the problematic value, and the two likely intents (-1 and 1).
/// </summary>
public class SubscriptionBudgetZeroOrBelowMinusOneValidationTests
{
    [Fact]
    public void When_subscription_budget_is_zero_should_report_one_warning_naming_value_and_likely_intents()
    {
        // Arrange — requeueCount: 0 is a zero-budget that fires on the first deferral
        var subscription = new Subscription(
            subscriptionName: new SubscriptionName("zero-budget-sub"),
            channelName: new ChannelName("zero-budget-channel"),
            routingKey: new RoutingKey("zero.budget.routing.key"),
            requestType: typeof(MyBareRequest),
            messagePumpType: MessagePumpType.Reactor,
            requeueCount: 0
        );

        var spec = ConsumerValidationRules.ZeroBudget();

        // Act
        var satisfied = spec.IsSatisfiedBy(subscription);
        var collector = new ValidationResultCollector<Subscription>();
        var results = spec.Accept(collector).ToList();

        // Assert — exactly one Warning naming the value 0 and both likely intents -1 and 1
        Assert.False(satisfied);
        Assert.Single(results);
        Assert.Equal(ValidationSeverity.Warning, results[0].Error!.Severity);
        Assert.Contains("0", results[0].Error!.Message);
        Assert.Contains("-1", results[0].Error!.Message);
        Assert.Contains("1", results[0].Error!.Message);
    }

    [Fact]
    public void When_subscription_budget_is_below_minus_one_should_report_one_warning_naming_value_and_likely_intents()
    {
        // Arrange — requeueCount: -3 is below -1, another zero-budget variant
        var subscription = new Subscription(
            subscriptionName: new SubscriptionName("negative-budget-sub"),
            channelName: new ChannelName("negative-budget-channel"),
            routingKey: new RoutingKey("negative.budget.routing.key"),
            requestType: typeof(MyBareRequest),
            messagePumpType: MessagePumpType.Reactor,
            requeueCount: -3
        );

        var spec = ConsumerValidationRules.ZeroBudget();

        // Act
        var satisfied = spec.IsSatisfiedBy(subscription);
        var collector = new ValidationResultCollector<Subscription>();
        var results = spec.Accept(collector).ToList();

        // Assert — exactly one Warning naming the value -3 and both likely intents -1 and 1
        Assert.False(satisfied);
        Assert.Single(results);
        Assert.Equal(ValidationSeverity.Warning, results[0].Error!.Severity);
        Assert.Contains("-3", results[0].Error!.Message);
        Assert.Contains("-1", results[0].Error!.Message);
        Assert.Contains("1", results[0].Error!.Message);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(-1)]
    public void When_subscription_budget_is_valid_should_not_report_zero_budget_warning(int requeueCount)
    {
        // Arrange — 3 is a normal budget; -1 is "requeue for ever" (never consulted by pump)
        var subscription = new Subscription(
            subscriptionName: new SubscriptionName("valid-budget-sub"),
            channelName: new ChannelName("valid-budget-channel"),
            routingKey: new RoutingKey("valid.budget.routing.key"),
            requestType: typeof(MyBareRequest),
            messagePumpType: MessagePumpType.Reactor,
            requeueCount: requeueCount
        );

        var spec = ConsumerValidationRules.ZeroBudget();

        // Act
        var satisfied = spec.IsSatisfiedBy(subscription);

        // Assert — no zero-budget finding for valid values
        Assert.True(satisfied);
    }
}
