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
/// Verifies that the R-7 zero-budget validation rule (ADR 0077) is registered by
/// <see cref="ServiceCollectionExtensions.AddConsumers"/> and surfaces a Warning — not an Error
/// — when a subscription has <c>requeueCount: 0</c>, so the host starts (AC-32).
/// </summary>
public class ValidatePipelinesZeroBudgetTests
{
    [Fact]
    public void When_validate_pipelines_with_zero_budget_should_surface_warning_and_start_host()
    {
        // Arrange — subscription with requeueCount: 0; handler registered so no "no handler" error obscures the test
        var services = new ServiceCollection();
        services
            .AddConsumers(options =>
            {
                options.Subscriptions =
                [
                    new Subscription<TestEvent>(
                        new SubscriptionName("zero-budget-sub"),
                        new ChannelName("zero-budget-channel"),
                        new RoutingKey("zero.budget.event"),
                        messagePumpType: MessagePumpType.Reactor,
                        requeueCount: 0)
                ];
            })
            .Handlers(r => r.Register<TestEvent, TestEventHandler>())
            .ValidatePipelines(throwOnError: true);

        var provider = services.BuildServiceProvider();

        // Act
        var result = PipelineValidationResult.Combine(
            provider.GetServices<IAmAPipelineValidator>().Select(v => v.Validate()).ToArray());

        // Assert — zero budget produces exactly one Warning; no Errors; IsValid is true (host starts)
        Assert.True(result.IsValid);
        Assert.Single(result.Warnings);
        var warning = result.Warnings.First();
        Assert.Contains("0", warning.Message);
        Assert.Contains("-1", warning.Message);
        Assert.Contains("1", warning.Message);
    }
}
