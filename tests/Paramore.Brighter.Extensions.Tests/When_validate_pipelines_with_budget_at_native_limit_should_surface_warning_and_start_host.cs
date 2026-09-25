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
/// Verifies that the R-10 native-redrive-limit validation rule (ADR 0077) is registered by
/// <see cref="ServiceCollectionExtensions.AddConsumers"/> and surfaces a Warning — not an Error
/// — when a subscription's budget meets its native redrive limit, so the host still starts (AC-10).
/// </summary>
public class ValidatePipelinesBudgetAtNativeLimitTests
{
    [Fact]
    public void When_validate_pipelines_with_budget_at_native_limit_should_surface_warning_and_start_host()
    {
        // Arrange — subscription with requeueCount: 5 and NativeRedriveLimit: 5 (R == M)
        var services = new ServiceCollection();
        services
            .AddConsumers(options =>
            {
                options.Subscriptions =
                [
                    new NativeRedriveEventSubscription(
                        subscriptionName: new SubscriptionName("native-limit-sub"),
                        channelName: new ChannelName("native-limit-channel"),
                        routingKey: new RoutingKey("native.limit.event"),
                        requeueCount: 5,
                        nativeRedriveLimit: 5,
                        messagePumpType: MessagePumpType.Reactor)
                ];
            })
            .Handlers(r => r.Register<TestEvent, TestEventHandler>())
            .ValidatePipelines(throwOnError: true);

        var provider = services.BuildServiceProvider();

        // Act
        var validator = provider.GetRequiredService<IAmAPipelineValidator>();
        var result = validator.Validate();

        // Assert — budget at native limit produces exactly one Warning; no Errors; host starts
        Assert.True(result.IsValid);
        Assert.Single(result.Warnings);
        var warning = result.Warnings.First();
        Assert.Contains("5", warning.Message);
        Assert.Contains("native", warning.Message, System.StringComparison.OrdinalIgnoreCase);
    }
}
