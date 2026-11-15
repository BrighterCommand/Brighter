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

namespace Paramore.Brighter.Extensions.Tests;

[NotInParallel]
public class ConsumerRestartExcessAndNoneExpectedTests
{
    private static readonly TimeSpan s_guard = TimeSpan.FromSeconds(10);

    [Test]
    public async Task When_more_consumers_run_than_expected_should_report_healthy()
    {
        //Arrange
        await using var host = new MultiSubscriptionReceiveHealthTestHost([new ReceiveHealthSubscription("orders", 3)]);
        host.Dispatcher.Receive();
        await ConsumersRunning(host, 3);
        var healthCheck = new ConsumerRestartHealthCheck(host.Dispatcher);

        //Act
        ((Dispatcher)host.Dispatcher).SetActivePerformers("orders", 2);
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        //Assert
        await Assert.That(host.Dispatcher.Consumers.Count()).IsEqualTo(3);
        await Assert.That(result.Status).IsEqualTo(HealthStatus.Healthy);
        await Assert.That(result.Description).Contains("2 expected consumers are running");
        await Assert.That(result.Data["remedy_kind"]).IsEqualTo(RemedyCheckConvention.Name(RemedyKind.Restart));
        await Assert.That((((IEnumerable<string>)(((IEnumerable<string>)(await Assert.That(result.Data["conditions"]).IsAssignableTo<IEnumerable<string>>())))))).IsEmpty();
    }

    [Test]
    public async Task When_no_consumers_are_expected_and_none_run_should_report_healthy()
    {
        //Arrange
        await using var host = new MultiSubscriptionReceiveHealthTestHost([new ReceiveHealthSubscription("orders", 0)]);
        host.Dispatcher.Receive();
        var healthCheck = new ConsumerRestartHealthCheck(host.Dispatcher);

        //Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        //Assert
        await Assert.That(host.Dispatcher.Consumers).IsEmpty();
        await Assert.That(result.Status).IsEqualTo(HealthStatus.Healthy);
        await Assert.That(result.Data["remedy_kind"]).IsEqualTo(RemedyCheckConvention.Name(RemedyKind.Restart));
        await Assert.That((((IEnumerable<string>)(((IEnumerable<string>)(await Assert.That(result.Data["conditions"]).IsAssignableTo<IEnumerable<string>>())))))).IsEmpty();
    }

    [Test]
    public async Task When_set_active_performers_starts_only_one_of_two_subscriptions_should_report_degraded()
    {
        //Arrange
        await using var host = new MultiSubscriptionReceiveHealthTestHost(
            [new ReceiveHealthSubscription("orders", 1), new ReceiveHealthSubscription("payments", 1)]);
        var healthCheck = new ConsumerRestartHealthCheck(host.Dispatcher);

        //Act
        ((Dispatcher)host.Dispatcher).SetActivePerformers("orders", 1);
        await ConsumersRunning(host, 1);
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        //Assert
        await Assert.That(result.Status).IsEqualTo(HealthStatus.Degraded);
        await Assert.That(result.Description).Contains("payments has 0 of 1 expected consumers");
        await Assert.That(result.Data["remedy_kind"]).IsEqualTo(RemedyCheckConvention.Name(RemedyKind.Restart));
        var condition = ((IDictionary<string, object>)(((IDictionary<string, object>)(await Assert.That(result.Data["missing_consumers"]).IsAssignableTo<IDictionary<string, object>>()))));
        await Assert.That(condition["running_total"]).IsEqualTo(1);
        await Assert.That(condition["expected_total"]).IsEqualTo(2);
    }

    private static async Task ConsumersRunning(MultiSubscriptionReceiveHealthTestHost host, int expected)
    {
        using var timeout = new System.Threading.CancellationTokenSource(s_guard);
        while (host.Dispatcher.Consumers.Count() != expected)
            await Task.Delay(5, timeout.Token);
    }
}
