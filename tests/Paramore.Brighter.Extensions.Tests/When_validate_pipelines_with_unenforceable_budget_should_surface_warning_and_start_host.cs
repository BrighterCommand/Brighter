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
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Paramore.Brighter.Validation;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

/// <summary>
/// Verifies that the R-11 unenforceable-budget validation rule (ADR 0077) is registered by
/// <see cref="ServiceCollectionExtensions.AddConsumers"/> and surfaces a Warning — not an Error
/// — when a subscription's delivery count cannot advance, so the host still starts (AC-11).
/// </summary>
public class ValidatePipelinesUnenforceableBudgetTests
{
    [Fact]
    public void When_validate_pipelines_with_unenforceable_budget_should_surface_warning_and_start_host()
    {
        // Arrange — subscription with requeueCount: 3 and unenforceability reason "no DeadLetterPolicy"
        var services = new ServiceCollection();
        services
            .AddConsumers(options =>
            {
                options.Subscriptions =
                [
                    new UnenforceableBudgetEventSubscription(
                        subscriptionName: new SubscriptionName("unenforceable-budget-sub"),
                        channelName: new ChannelName("unenforceable-budget-channel"),
                        routingKey: new RoutingKey("unenforceable.budget.event"),
                        requeueCount: 3,
                        deliveryBudgetUnenforceableReason: "no DeadLetterPolicy",
                        messagePumpType: MessagePumpType.Reactor)
                ];
            })
            .Handlers(r => r.Register<TestEvent, TestEventHandler>())
            .ValidatePipelines(throwOnError: true);

        var provider = services.BuildServiceProvider();

        // Act
        var validator = provider.GetRequiredService<IAmAPipelineValidator>();
        var result = validator.Validate();

        // Assert — unenforceable budget produces exactly one Warning; no Errors; host starts
        Assert.True(result.IsValid);
        Assert.Single(result.Warnings);
        var warning = result.Warnings.First();
        Assert.Contains("3", warning.Message);
        Assert.Contains("no DeadLetterPolicy", warning.Message);
    }
}
