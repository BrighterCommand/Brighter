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

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.Hosting;
using Paramore.Brighter.Validation;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

/// <summary>
/// Verifies that all three budget validation rules (R-7, R-10, R-11; ADR 0077) are registered by
/// <see cref="ServiceCollectionExtensions.AddConsumers"/> with Warning severity, so the
/// <see cref="ServiceActivatorHostedService"/> actually starts — <see cref="StartAsync"/> does not
/// throw — even when <c>throwOnError: true</c> is set (R-25, R-26, AC-32).
/// </summary>
public class ThrowOnErrorTrueWithOnlyBudgetFindingsTests
{
    [Fact]
    public async Task When_throw_on_error_true_with_only_budget_findings_should_start_host()
    {
        // Arrange — one subscription per budget rule; handler registered so no "no handler" error obscures the test
        var actionLog = new List<string>();
        var services = new ServiceCollection();
        services
            .AddConsumers(options =>
            {
                options.Subscriptions =
                [
                    // R-7: requeueCount 0 is a zero-budget value; plain Subscription passes R-10 and R-11 vacuously
                    new Subscription<TestEvent>(
                        new SubscriptionName("zero-budget-sub"),
                        new ChannelName("zero-budget-channel"),
                        new RoutingKey("zero.budget.event"),
                        messagePumpType: MessagePumpType.Reactor,
                        requeueCount: 0),

                    // R-10: requeueCount 5 meets NativeRedriveLimit 5; passes R-7 (5 >= 1) and R-11 (reason is null)
                    new NativeRedriveEventSubscription(
                        subscriptionName: new SubscriptionName("native-limit-sub"),
                        channelName: new ChannelName("native-limit-channel"),
                        routingKey: new RoutingKey("native.limit.event"),
                        requeueCount: 5,
                        nativeRedriveLimit: 5,
                        messagePumpType: MessagePumpType.Reactor),

                    // R-11: requeueCount 3 with non-null reason; passes R-7 (3 >= 1) and R-10 (NativeRedriveLimit is null)
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

        // Provide a spy dispatcher so the hosted service can call Receive() without real channels
        services.AddSingleton<IDispatcher>(new SpyDispatcher(actionLog));

        var provider = services.BuildServiceProvider();

        var logger = NullLogger<ServiceActivatorHostedService>.Instance;
        var dispatcher = provider.GetRequiredService<IDispatcher>();
        var options = provider.GetRequiredService<IOptions<BrighterPipelineValidationOptions>>();
        var service = new ServiceActivatorHostedService(logger, dispatcher, provider, options);

        // Act — start the hosted service; budget warnings must not cause a PipelineValidationException
        var exception = await Record.ExceptionAsync(() => service.StartAsync(CancellationToken.None));

        // Assert — primary: host starts without a PipelineValidationException (R-25, R-26, AC-32)
        Assert.Null(exception);

        // Supporting evidence: the real validator sees three Warning findings and no Errors
        var validator = provider.GetRequiredService<IAmAPipelineValidator>();
        var result = validator.Validate();
        Assert.True(result.IsValid);
        Assert.Equal(3, result.Warnings.Count);
        Assert.Empty(result.Errors);

        // Teardown
        await service.StopAsync(CancellationToken.None);
    }
}
