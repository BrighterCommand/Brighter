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

[Collection(DispatcherHealthCheckCollection.NAME)]
public class ConsumerRestartDispatcherNotRunningTests
{
    private static readonly TimeSpan s_guard = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task When_the_dispatcher_is_awaiting_should_report_healthy_and_say_it_is_not_running()
    {
        //Arrange
        await using var host = new MultiSubscriptionReceiveHealthTestHost([new ReceiveHealthSubscription("orders", 2)]);
        var healthCheck = new ConsumerRestartHealthCheck(host.Dispatcher);

        //Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        //Assert
        AssertHealthyNotRunning(result);
    }

    [Fact]
    public async Task When_end_has_been_called_should_report_healthy_while_stopping_and_once_stopped()
    {
        //Arrange
        await using var host = new MultiSubscriptionReceiveHealthTestHost([new ReceiveHealthSubscription("orders", 2)]);
        host.Dispatcher.Receive();
        await ConsumersRunning(host, 2);
        var healthCheck = new ConsumerRestartHealthCheck(host.Dispatcher);

        //Act
        var ending = host.Dispatcher.End();
        var whileStopping = await healthCheck.CheckHealthAsync(new HealthCheckContext());
        RecoverAll(host);
        await ending.WaitAsync(s_guard);
        var whenStopped = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        //Assert
        AssertHealthyNotRunning(whileStopping);
        AssertHealthyNotRunning(whenStopped);
    }

    [Fact]
    public async Task When_the_dispatcher_is_restarted_with_a_missing_consumer_should_report_degraded()
    {
        //Arrange
        await using var host = new MultiSubscriptionReceiveHealthTestHost([new ReceiveHealthSubscription("orders", 2)]);
        host.Dispatcher.Receive();
        await ConsumersRunning(host, 2);
        var healthCheck = new ConsumerRestartHealthCheck(host.Dispatcher);
        var ending = host.Dispatcher.End();
        RecoverAll(host);
        await ending.WaitAsync(s_guard);
        var whileEnded = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        //Act
        host.Dispatcher.Receive();
        await ConsumersRunning(host, 2);
        host.StopPerformer("orders", 2);
        await ConsumersRunning(host, 1);
        var restarted = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        //Assert
        AssertHealthyNotRunning(whileEnded);
        Assert.Equal(HealthStatus.Degraded, restarted.Status);
        Assert.Contains("orders has 1 of 2 expected consumers", restarted.Description);
        Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Restart), restarted.Data["remedy_kind"]);
    }

    [Fact]
    public async Task When_the_dispatcher_stopped_by_itself_and_end_is_then_called_should_report_healthy()
    {
        //Arrange
        await using var host = new MultiSubscriptionReceiveHealthTestHost([new ReceiveHealthSubscription("orders", 2)]);
        host.Dispatcher.Receive();
        await ConsumersRunning(host, 2);
        var healthCheck = new ConsumerRestartHealthCheck(host.Dispatcher);
        host.StopPerformer("orders", 0);
        host.StopPerformer("orders", 1);
        await DispatcherStopped(host);

        //Act
        var stoppedByItself = await healthCheck.CheckHealthAsync(new HealthCheckContext());
        await host.Dispatcher.End().WaitAsync(s_guard);
        var afterEnd = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        //Assert
        Assert.Equal(DispatcherState.DS_STOPPED, ((Dispatcher)host.Dispatcher).State);
        Assert.Equal(HealthStatus.Unhealthy, stoppedByItself.Status);
        Assert.Contains("orders has 0 of 2 expected consumers", stoppedByItself.Description);
        AssertHealthyNotRunning(afterEnd);
    }

    private static void AssertHealthyNotRunning(HealthCheckResult result)
    {
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("not running", result.Description);
        Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Restart), result.Data["remedy_kind"]);
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<string>>(result.Data["conditions"]));
    }

    private static void RecoverAll(MultiSubscriptionReceiveHealthTestHost host)
    {
        foreach (var transport in host.Transports("orders"))
            transport.Recover();
    }

    private static async Task ConsumersRunning(MultiSubscriptionReceiveHealthTestHost host, int expected)
    {
        using var timeout = new System.Threading.CancellationTokenSource(s_guard);
        while (host.Dispatcher.Consumers.Count() != expected)
            await Task.Delay(5, timeout.Token);
    }

    private static async Task DispatcherStopped(MultiSubscriptionReceiveHealthTestHost host)
    {
        using var timeout = new System.Threading.CancellationTokenSource(s_guard);
        while (((Dispatcher)host.Dispatcher).State != DispatcherState.DS_STOPPED)
            await Task.Delay(5, timeout.Token);
    }
}
