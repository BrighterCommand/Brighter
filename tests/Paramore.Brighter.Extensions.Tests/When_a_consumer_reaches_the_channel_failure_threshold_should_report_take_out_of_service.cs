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

namespace Paramore.Brighter.Extensions.Tests;

[NotInParallel]
public class ConsumerTakeOutOfServiceChannelFailureTests
{
    private static readonly TimeSpan s_guard = TimeSpan.FromSeconds(10);

    [Test]
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
        await Assert.That(belowResult.Status).IsEqualTo(HealthStatus.Healthy);
        await Assert.That(belowResult.Description).Contains("absent");
        await Assert.That(belowResult.Data["remedy_kind"]).IsEqualTo("take_out_of_service");
        await Assert.That((((IEnumerable<string>)(((IEnumerable<string>)(await Assert.That(belowResult.Data["conditions"]).IsAssignableTo<IEnumerable<string>>())))))).IsEmpty();

        await Assert.That(atResult.Status).IsEqualTo(HealthStatus.Degraded);
        var failing = await Assert.That(System.Linq.Enumerable.Where(at.Dispatcher.Consumers, consumer => consumer.Performer is IHaveAChannelFailureCount
        {
            ConsecutiveChannelFailures: 3
        })).HasSingleItem();
        await Assert.That(atResult.Description).Contains("orders");
        await Assert.That(atResult.Description).Contains(failing.Name.Value);
        await Assert.That(atResult.Description).Contains("3");
        await Assert.That(atResult.Data["remedy_kind"]).IsEqualTo("take_out_of_service");
        await Assert.That((((IEnumerable<string>)(((IEnumerable<string>)(await Assert.That(atResult.Data["conditions"]).IsAssignableTo<IEnumerable<string>>())))))).IsEqualTo(new[] { "persistent_channel_failures" });
        var condition = ((IDictionary<string, object>)(((IDictionary<string, object>)(await Assert.That(atResult.Data["persistent_channel_failures"]).IsAssignableTo<IDictionary<string, object>>()))));
        await Assert.That(condition["threshold"]).IsEqualTo(3);
        var consumer = await Assert.That((((IEnumerable<IDictionary<string, object>>)(((IEnumerable<IDictionary<string, object>>)(await Assert.That(condition["consumers"]).IsAssignableTo<IEnumerable<IDictionary<string, object>>>())))))).HasSingleItem();
        await Assert.That(consumer["subscription"]).IsEqualTo("orders");
        await Assert.That(consumer["consumer"]).IsEqualTo(failing.Name.Value);
        await Assert.That(consumer["count"]).IsEqualTo(3);
    }

    [Test]
    public async Task When_no_consumer_is_running_the_take_out_of_service_check_should_not_report_unhealthy()
    {
        //Arrange
        await using var host = new MultiSubscriptionReceiveHealthTestHost([new ReceiveHealthSubscription("orders", 3)]);
        var healthCheck = new ConsumerTakeOutOfServiceHealthCheck(host.Dispatcher, 3);

        //Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        //Assert
        await Assert.That(result.Status).IsNotEqualTo(HealthStatus.Unhealthy);
        await Assert.That(result.Data["remedy_kind"]).IsEqualTo("take_out_of_service");
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
