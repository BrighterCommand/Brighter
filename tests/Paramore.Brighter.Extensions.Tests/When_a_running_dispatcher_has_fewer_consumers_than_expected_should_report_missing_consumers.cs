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
using Paramore.Brighter.ServiceActivator.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

[Collection(DispatcherHealthCheckCollection.NAME)]
public class ConsumerRestartMissingConsumersTests
{
    private static readonly TimeSpan s_guard = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task When_a_running_dispatcher_has_fewer_consumers_than_expected_should_report_missing_consumers()
    {
        //Arrange
        await using var host = new MultiSubscriptionReceiveHealthTestHost([new ReceiveHealthSubscription("orders", 2)]);
        host.Dispatcher.Receive();
        await ConsumersRunning(host, 2);
        var healthCheck = new ConsumerRestartHealthCheck(host.Dispatcher);

        //Act
        var allRunning = await healthCheck.CheckHealthAsync(new HealthCheckContext());
        host.StopPerformer("orders", 1);
        await ConsumersRunning(host, 1);
        var oneMissing = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        //Assert
        Assert.Equal(HealthStatus.Healthy, allRunning.Status);
        Assert.Contains("2", allRunning.Description);
        Assert.Contains("expected consumers are running", allRunning.Description);
        Assert.Equal("restart", allRunning.Data["remedy_kind"]);

        Assert.Equal(HealthStatus.Degraded, oneMissing.Status);
        Assert.Contains("orders", oneMissing.Description);
        Assert.Contains("1", oneMissing.Description);
        Assert.Contains("2", oneMissing.Description);
        Assert.Equal("restart", oneMissing.Data["remedy_kind"]);
        Assert.Equal(new[] { "missing_consumers" }, Assert.IsAssignableFrom<IEnumerable<string>>(oneMissing.Data["conditions"]));
        var condition = Assert.IsAssignableFrom<IDictionary<string, object>>(oneMissing.Data["missing_consumers"]);
        Assert.Equal(1, condition["running_total"]);
        Assert.Equal(2, condition["expected_total"]);
        var subscription = Assert.Single(Assert.IsAssignableFrom<IEnumerable<IDictionary<string, object>>>(condition["subscriptions"]));
        Assert.Equal("orders", subscription["subscription"]);
        Assert.Equal(1, subscription["running"]);
        Assert.Equal(2, subscription["expected"]);
        Assert.Equal(0, oneMissing.Data["consumer_operations_in_flight"]);
    }

    private static async Task ConsumersRunning(MultiSubscriptionReceiveHealthTestHost host, int expected)
    {
        using var timeout = new System.Threading.CancellationTokenSource(s_guard);
        while (host.Dispatcher.Consumers.Count() != expected)
            await Task.Delay(5, timeout.Token);
    }
}
