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
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessagingGateway;

/// <summary>
/// Tests for <see cref="DeliveryBudgetDiagnostics.WarnIfUnenforceable"/> (R-26, AC-11):
/// channel creation logs exactly one Warning when the delivery budget is unenforceable, and
/// nothing in all other cases — including shapes that trip R-7 and R-10 in pipeline validation.
/// </summary>
public class DeliveryBudgetDiagnosticsTests
{
    private const string SubscriptionNameValue = "unenforceable-sub";
    private const string UnenforceableReason = "no DeadLetterPolicy";

    [Fact]
    public void When_channel_created_for_unenforceable_budget_should_log_warning_once()
    {
        //Arrange
        var sub = new UnenforceableDeliveryCountingSubscriptionDouble(
            subscriptionName: new SubscriptionName(SubscriptionNameValue),
            channelName: new ChannelName("unenforceable-channel"),
            routingKey: new RoutingKey("unenforceable.routing.key"),
            requeueCount: 3,
            deliveryBudgetUnenforceableReason: UnenforceableReason
        );

        using var ctx = TestCorrelator.CreateContext();

        //Act
        DeliveryBudgetDiagnostics.WarnIfUnenforceable(sub);

        //Assert — exactly one Warning naming the subscription, 3, and the reason
        var warnings = TestCorrelator.GetLogEventsFromCurrentContext()
            .Where(e => e.Level == LogEventLevel.Warning)
            .ToList();

        Assert.Single(warnings);
        var rendered = warnings[0].RenderMessage();
        Assert.Contains(SubscriptionNameValue, rendered);
        Assert.Contains("3", rendered);
        Assert.Contains(UnenforceableReason, rendered);
    }

    [Fact]
    public void When_channel_created_for_enforceable_budget_should_log_nothing()
    {
        //Arrange — null reason means the budget is enforceable; no warning expected
        var sub = new UnenforceableDeliveryCountingSubscriptionDouble(
            subscriptionName: new SubscriptionName("enforceable-sub"),
            channelName: new ChannelName("enforceable-channel"),
            routingKey: new RoutingKey("enforceable.routing.key"),
            requeueCount: 3,
            deliveryBudgetUnenforceableReason: null
        );

        using var ctx = TestCorrelator.CreateContext();

        //Act
        DeliveryBudgetDiagnostics.WarnIfUnenforceable(sub);

        //Assert — no log events at any level
        var events = TestCorrelator.GetLogEventsFromCurrentContext().ToList();
        Assert.Empty(events);
    }

    [Fact]
    public void When_channel_created_with_unlimited_budget_and_unenforceable_reason_should_log_nothing()
    {
        //Arrange — requeueCount -1 disables the budget (R-6); warning must not fire even with a reason
        var sub = new UnenforceableDeliveryCountingSubscriptionDouble(
            subscriptionName: new SubscriptionName("unlimited-sub"),
            channelName: new ChannelName("unlimited-channel"),
            routingKey: new RoutingKey("unlimited.routing.key"),
            requeueCount: -1,
            deliveryBudgetUnenforceableReason: UnenforceableReason
        );

        using var ctx = TestCorrelator.CreateContext();

        //Act
        DeliveryBudgetDiagnostics.WarnIfUnenforceable(sub);

        //Assert — no log events at any level
        var events = TestCorrelator.GetLogEventsFromCurrentContext().ToList();
        Assert.Empty(events);
    }

    [Fact]
    public void When_channel_created_for_plain_subscription_should_log_nothing()
    {
        //Arrange — a plain Subscription that does not implement IAmADeliveryCountingSubscription
        var sub = new Subscription(
            subscriptionName: new SubscriptionName("plain-sub"),
            channelName: new ChannelName("plain-channel"),
            routingKey: new RoutingKey("plain.routing.key"),
            requestType: typeof(MyBareRequest),
            messagePumpType: MessagePumpType.Reactor,
            requeueCount: 3
        );

        using var ctx = TestCorrelator.CreateContext();

        //Act
        DeliveryBudgetDiagnostics.WarnIfUnenforceable(sub);

        //Assert — no log events at any level
        var events = TestCorrelator.GetLogEventsFromCurrentContext().ToList();
        Assert.Empty(events);
    }

    [Fact]
    public void When_channel_created_for_zero_requeue_count_should_not_log_r7_text()
    {
        //Arrange — requeueCount 0 would trip R-7 in pipeline validation, but R-26 gives
        //           channel-creation logging to R-11 only; reason null means enforceable
        var sub = new DeliveryCountingSubscriptionDouble(
            subscriptionName: new SubscriptionName("zero-budget-sub"),
            channelName: new ChannelName("zero-budget-channel"),
            routingKey: new RoutingKey("zero.budget.routing.key"),
            requeueCount: 0,
            nativeRedriveLimit: null
        );

        using var ctx = TestCorrelator.CreateContext();

        //Act
        DeliveryBudgetDiagnostics.WarnIfUnenforceable(sub);

        //Assert — R-7 is a pipeline-validation concern; no log here
        var events = TestCorrelator.GetLogEventsFromCurrentContext().ToList();
        Assert.Empty(events);
    }

    [Fact]
    public void When_channel_created_for_budget_meeting_native_limit_should_not_log_r10_text()
    {
        //Arrange — budget >= native limit trips R-10 in pipeline validation, but R-26 gives
        //           channel-creation logging to R-11 only; reason null means enforceable
        var sub = new DeliveryCountingSubscriptionDouble(
            subscriptionName: new SubscriptionName("native-limit-sub"),
            channelName: new ChannelName("native-limit-channel"),
            routingKey: new RoutingKey("native.limit.routing.key"),
            requeueCount: 3,
            nativeRedriveLimit: 3
        );

        using var ctx = TestCorrelator.CreateContext();

        //Act
        DeliveryBudgetDiagnostics.WarnIfUnenforceable(sub);

        //Assert — R-10 is a pipeline-validation concern; no log here
        var events = TestCorrelator.GetLogEventsFromCurrentContext().ToList();
        Assert.Empty(events);
    }
}
