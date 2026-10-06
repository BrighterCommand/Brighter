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
using Paramore.Brighter.ServiceActivator.Validation;
using Paramore.Brighter.Gcp.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.GcpPubSub;
using Paramore.Brighter.Validation;
using Xunit;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway;

/// <summary>
/// Verifies that a real <see cref="GcpPubSubSubscription"/> with no <see cref="DeadLetterPolicy"/> and a
/// configured delivery budget (<c>requeueCount: 3</c>) trips R-11's <c>UnenforceableBudget</c> rule under
/// startup pipeline validation — exactly one <see cref="ValidationSeverity.Warning"/> finding, naming the
/// subscription, the configured budget, and the unenforceable reason — and no other budget rule fires
/// (R-11, R-25, AC-11 first clause, ADR 0077).
/// </summary>
public class GcpPubSubSubscriptionValidationWarningTests
{
    [Fact]
    public void When_validating_a_gcp_subscription_without_dead_letter_policy_should_report_one_warning()
    {
        // Arrange — a real GcpPubSubSubscription with no DeadLetterPolicy and requeueCount: 3
        var pipelineBuilder = new PipelineBuilder<IRequest>(new SubscriberRegistry());

        var subscription = new GcpPubSubSubscription<MyCommand>(
            subscriptionName: new SubscriptionName("no-dlq-sub"),
            channelName: new ChannelName("no-dlq-channel"),
            routingKey: new RoutingKey("orders"),
            requeueCount: 3,
            messagePumpType: MessagePumpType.Reactor);

        var consumerRules = new ISpecification<Subscription>[]
        {
            ConsumerValidationRules.ZeroBudget(),
            ConsumerValidationRules.BudgetAtNativeRedriveLimit(),
            ConsumerValidationRules.UnenforceableBudget()
        };

        var validator = new PipelineValidator(pipelineBuilder, subscriptions: [subscription], consumerSpecs: consumerRules);

        // Act
        var result = validator.Validate();

        // Assert — exactly one Warning, naming the subscription, the budget, and the reason; no Errors
        Assert.True(result.IsValid);
        Assert.Single(result.Warnings);
        var warning = result.Warnings.First();
        Assert.Equal(ValidationSeverity.Warning, warning.Severity);
        Assert.Contains(subscription.Name.ToString(), warning.Message);
        Assert.Contains("3", warning.Message);
        Assert.Contains("DeadLetterPolicy", warning.Message);
    }
}
