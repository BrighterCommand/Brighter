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
/// Verifies that running all three budget validation rules (R-7, R-10, R-11; ADR 0077) through a
/// <see cref="PipelineValidator"/> produces exactly three Warning findings — one per rule — and no
/// Errors, so <see cref="PipelineValidationResult.IsValid"/> remains true (R-25, R-26, AC-32); and that a
/// subscription tripping two budget rules produces two findings (AC-29).
/// </summary>
public class PipelineValidatorBudgetOnlyWarningsTests
{
    [Fact]
    public void When_only_budget_rules_fire_should_report_only_warning_findings()
    {
        // Arrange — one subscription per budget rule; none of the three budget rules is an Error
        var registry = new SubscriberRegistry();
        var pipelineBuilder = new PipelineBuilder<IRequest>(registry);
        PipelineBuilder<IRequest>.ClearPipelineCache();

        var subscriptions = new Subscription[]
        {
            // R-7: requeueCount 0 is a zero-budget value; plain Subscription passes R-10 and R-11 vacuously
            new Subscription(
                subscriptionName: new SubscriptionName("zero-budget-sub"),
                channelName: new ChannelName("zero-budget-channel"),
                routingKey: new RoutingKey("zero.budget.routing.key"),
                requestType: typeof(MyBareRequest),
                messagePumpType: MessagePumpType.Reactor,
                requeueCount: 0),

            // R-10: requeueCount 5 meets NativeRedriveLimit 5; passes R-7 (5 >= 1) and R-11 (reason is null)
            new DeliveryCountingSubscriptionDouble(
                subscriptionName: new SubscriptionName("native-limit-sub"),
                channelName: new ChannelName("native-limit-channel"),
                routingKey: new RoutingKey("native.limit.routing.key"),
                requeueCount: 5,
                nativeRedriveLimit: 5),

            // R-11: requeueCount 3 with non-null reason; passes R-7 (3 >= 1) and R-10 (NativeRedriveLimit is null)
            new UnenforceableDeliveryCountingSubscriptionDouble(
                subscriptionName: new SubscriptionName("unenforceable-sub"),
                channelName: new ChannelName("unenforceable-channel"),
                routingKey: new RoutingKey("unenforceable.routing.key"),
                requeueCount: 3,
                deliveryBudgetUnenforceableReason: "no DeadLetterPolicy")
        };

        var consumerRules = new ISpecification<Subscription>[]
        {
            ConsumerValidationRules.ZeroBudget(),
            ConsumerValidationRules.BudgetAtNativeRedriveLimit(),
            ConsumerValidationRules.UnenforceableBudget()
        };

        var validator = new PipelineValidator(pipelineBuilder, subscriptions: subscriptions, consumerSpecs: consumerRules);

        // Act
        var result = validator.Validate();

        // Assert — exactly three Warnings (one per budget rule), no Errors; result is valid (R-25, AC-32)
        Assert.True(result.IsValid);
        Assert.Equal(3, result.Warnings.Count);
        Assert.Empty(result.Errors);
        Assert.All(result.Warnings, w => Assert.Equal(ValidationSeverity.Warning, w.Severity));
    }

    // AC-29: a single subscription that trips two budget rules produces exactly two Warning findings,
    // one per rule, and no Error
    [Fact]
    public void When_subscription_trips_two_budget_rules_should_produce_two_warning_findings()
    {
        // Arrange — requeueCount 0 trips R-7 (zero-budget: 0 != -1, 0 < 1) and R-11 (unenforceable:
        // 0 != -1, reason not null); NativeRedriveLimit null means R-10 passes vacuously
        var registry = new SubscriberRegistry();
        var pipelineBuilder = new PipelineBuilder<IRequest>(registry);
        PipelineBuilder<IRequest>.ClearPipelineCache();

        var subscriptions = new Subscription[]
        {
            new UnenforceableDeliveryCountingSubscriptionDouble(
                subscriptionName: new SubscriptionName("two-rule-sub"),
                channelName: new ChannelName("two-rule-channel"),
                routingKey: new RoutingKey("two.rule.routing.key"),
                requeueCount: 0,
                deliveryBudgetUnenforceableReason: "no DeadLetterPolicy")
        };

        var consumerRules = new ISpecification<Subscription>[]
        {
            ConsumerValidationRules.ZeroBudget(),
            ConsumerValidationRules.BudgetAtNativeRedriveLimit(),
            ConsumerValidationRules.UnenforceableBudget()
        };

        var validator = new PipelineValidator(pipelineBuilder, subscriptions: subscriptions, consumerSpecs: consumerRules);

        // Act
        var result = validator.Validate();

        // Assert — exactly two Warnings (R-7 and R-11), no Errors (AC-29)
        Assert.True(result.IsValid);
        Assert.Equal(2, result.Warnings.Count);
        Assert.Empty(result.Errors);
        Assert.All(result.Warnings, w => Assert.Equal(ValidationSeverity.Warning, w.Severity));
    }
}
