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
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.ServiceActivator.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

[Collection(DispatcherHealthCheckCollection.NAME)]
public class ConsumerTakeOutOfServiceChannelFailureTests
{
    private static readonly TimeSpan s_guard = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task When_a_consumer_reaches_the_channel_failure_threshold_should_report_take_out_of_service()
    {
        //Arrange
        await using var below = StartedHost([0, 2, 2], out var belowReady);
        await using var at = StartedHost([0, 3, 2], out var atReady);
        await belowReady.WaitAsync(s_guard);
        await atReady.WaitAsync(s_guard);
        var belowCheck = new ConsumerTakeOutOfServiceHealthCheck(below.Dispatcher, 3);
        var atCheck = new ConsumerTakeOutOfServiceHealthCheck(at.Dispatcher, 3);

        //Act
        var belowResult = await belowCheck.CheckHealthAsync(new HealthCheckContext());
        var atResult = await atCheck.CheckHealthAsync(new HealthCheckContext());

        //Assert
        Assert.Equal(HealthStatus.Healthy, belowResult.Status);
        Assert.Contains("absent", belowResult.Description);
        Assert.Equal("take_out_of_service", belowResult.Data["remedy_kind"]);
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<string>>(belowResult.Data["conditions"]));

        Assert.Equal(HealthStatus.Degraded, atResult.Status);
        var failing = Assert.Single(at.Dispatcher.Consumers, consumer => consumer.Performer is IHaveAChannelFailureCount
        {
            ConsecutiveChannelFailures: 3
        });
        Assert.Contains("orders", atResult.Description);
        Assert.Contains(failing.Name.Value, atResult.Description);
        Assert.Contains("3", atResult.Description);
        Assert.Equal("take_out_of_service", atResult.Data["remedy_kind"]);
        Assert.Equal(new[] { "persistent_channel_failures" }, Assert.IsAssignableFrom<IEnumerable<string>>(atResult.Data["conditions"]));
        var condition = Assert.IsAssignableFrom<IDictionary<string, object>>(atResult.Data["persistent_channel_failures"]);
        Assert.Equal(3, condition["threshold"]);
        var consumer = Assert.Single(Assert.IsAssignableFrom<IEnumerable<IDictionary<string, object>>>(condition["consumers"]));
        Assert.Equal("orders", consumer["subscription"]);
        Assert.Equal(failing.Name.Value, consumer["consumer"]);
        Assert.Equal(3, consumer["count"]);
    }

    [Fact]
    public async Task When_no_consumer_is_running_the_take_out_of_service_check_should_not_report_unhealthy()
    {
        //Arrange
        await using var host = new MultiSubscriptionReceiveHealthTestHost([new ReceiveHealthSubscription("orders", 3)]);
        var healthCheck = new ConsumerTakeOutOfServiceHealthCheck(host.Dispatcher, 3);

        //Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        //Assert
        Assert.NotEqual(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("take_out_of_service", result.Data["remedy_kind"]);
    }

    private static MultiSubscriptionReceiveHealthTestHost StartedHost(int[] failures, out Task ready)
    {
        var host = new MultiSubscriptionReceiveHealthTestHost([new ReceiveHealthSubscription("orders", failures.Length, failures)]);
        host.Dispatcher.Receive();
        ready = ConsumersObserved(host, failures.Length);
        return host;
    }

    private static async Task ConsumersObserved(MultiSubscriptionReceiveHealthTestHost host, int expected)
    {
        while (host.Transports("orders").Count < expected)
            await Task.Delay(5);
        await Task.WhenAll(host.Transports("orders").Select(transport => transport.FailuresObserved));
    }
}
