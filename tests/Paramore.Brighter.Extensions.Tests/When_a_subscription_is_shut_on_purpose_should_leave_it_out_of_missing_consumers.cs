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

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.ServiceActivator.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class ConsumerRestartShutSubscriptionTests
{
    private static readonly TimeSpan s_guard = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task When_the_only_subscription_is_shut_should_report_healthy_and_degraded_once_reopened_with_one_missing()
    {
        //Arrange
        await using var host = new MultiSubscriptionReceiveHealthTestHost([new ReceiveHealthSubscription("orders", 2)]);
        host.Dispatcher.Receive();
        await ConsumersRunning(host, 2);
        var healthCheck = new ConsumerRestartHealthCheck(host.Dispatcher);

        //Act
        ((Dispatcher)host.Dispatcher).Shut(new SubscriptionName("orders"));
        RecoverAll(host, "orders");
        await ConsumersRunning(host, 0);
        var shut = await healthCheck.CheckHealthAsync(new HealthCheckContext());
        ((Dispatcher)host.Dispatcher).Open(new SubscriptionName("orders"));
        await ConsumersRunning(host, 2);
        host.Transports("orders").Last().Quit();
        await ConsumersRunning(host, 1);
        var reopened = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        //Assert
        Assert.Equal(HealthStatus.Healthy, shut.Status);
        Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Restart), shut.Data["remedy_kind"]);
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<string>>(shut.Data["conditions"]));
        Assert.Equal(HealthStatus.Degraded, reopened.Status);
        Assert.Contains("orders has 1 of 2 expected consumers", reopened.Description);
        Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Restart), reopened.Data["remedy_kind"]);
    }

    [Fact]
    public async Task When_one_subscription_is_shut_and_another_is_missing_a_consumer_should_leave_the_shut_one_out()
    {
        //Arrange
        await using var host = new MultiSubscriptionReceiveHealthTestHost(
            [new ReceiveHealthSubscription("orders", 2), new ReceiveHealthSubscription("payments", 2)]);
        host.Dispatcher.Receive();
        await ConsumersRunning(host, 4);
        var healthCheck = new ConsumerRestartHealthCheck(host.Dispatcher);

        //Act
        ((Dispatcher)host.Dispatcher).Shut(new SubscriptionName("orders"));
        RecoverAll(host, "orders");
        host.StopPerformer("payments", 1);
        await ConsumersRunning(host, 1);
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        //Assert
        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("payments has 1 of 2 expected consumers", result.Description);
        Assert.DoesNotContain("orders", result.Description);
        Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Restart), result.Data["remedy_kind"]);
        var condition = Assert.IsAssignableFrom<IDictionary<string, object>>(result.Data["missing_consumers"]);
        Assert.Equal(1, condition["running_total"]);
        Assert.Equal(2, condition["expected_total"]);
        var subscription = Assert.Single(Assert.IsAssignableFrom<IEnumerable<IDictionary<string, object>>>(condition["subscriptions"]));
        Assert.Equal("payments", subscription["subscription"]);
    }

    [Fact]
    public async Task When_a_subscription_is_shut_while_awaiting_and_the_dispatcher_is_then_started_should_still_expect_it()
    {
        //Arrange
        await using var host = new MultiSubscriptionReceiveHealthTestHost(
            [new ReceiveHealthSubscription("orders", 2), new ReceiveHealthSubscription("payments", 1)]);
        var healthCheck = new ConsumerRestartHealthCheck(host.Dispatcher);

        //Act
        ((Dispatcher)host.Dispatcher).Shut(new SubscriptionName("orders"));
        ((Dispatcher)host.Dispatcher).SetActivePerformers("payments", 1);
        await ConsumersRunning(host, 1);
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        //Assert
        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("orders has 0 of 2 expected consumers", result.Description);
        Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Restart), result.Data["remedy_kind"]);
        var condition = Assert.IsAssignableFrom<IDictionary<string, object>>(result.Data["missing_consumers"]);
        var subscription = Assert.Single(Assert.IsAssignableFrom<IEnumerable<IDictionary<string, object>>>(condition["subscriptions"]));
        Assert.Equal("orders", subscription["subscription"]);
    }

    private static void RecoverAll(MultiSubscriptionReceiveHealthTestHost host, string subscription)
    {
        foreach (var transport in host.Transports(subscription))
            transport.Recover();
    }

    private static async Task ConsumersRunning(MultiSubscriptionReceiveHealthTestHost host, int expected)
    {
        using var timeout = new System.Threading.CancellationTokenSource(s_guard);
        while (host.Dispatcher.Consumers.Count() != expected)
            await Task.Delay(5, timeout.Token);
    }
}
