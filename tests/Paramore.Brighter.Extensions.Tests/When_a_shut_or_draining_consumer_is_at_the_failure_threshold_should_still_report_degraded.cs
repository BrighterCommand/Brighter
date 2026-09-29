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
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.ServiceActivator.Extensions.Diagnostics.HealthChecks;

namespace Paramore.Brighter.Extensions.Tests;

[NotInParallel]
public class ConsumerTakeOutOfServiceDispatcherStateTests
{
    private static readonly TimeSpan s_guard = TimeSpan.FromSeconds(10);

    [Test]
    public async Task When_a_shut_consumer_is_at_the_failure_threshold_should_still_report_degraded()
    {
        //Arrange
        await using var host = new MultiSubscriptionReceiveHealthTestHost([new ReceiveHealthSubscription("orders", 1, [3])]);
        host.Dispatcher.Receive();
        await ObservedFailures(host);
        var healthCheck = new ConsumerTakeOutOfServiceHealthCheck(host.Dispatcher, 3);

        //Act
        host.Dispatcher.Shut(new SubscriptionName("orders"));
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        //Assert
        var consumer = await Assert.That(host.Dispatcher.Consumers).HasSingleItem();
        await Assert.That(consumer.State).IsEqualTo(ConsumerState.Shut);
        await Assert.That((((IHaveAChannelFailureCount)(((IHaveAChannelFailureCount)(await Assert.That(consumer.Performer).IsAssignableTo<IHaveAChannelFailureCount>()))))).ConsecutiveChannelFailures).IsEqualTo(3);
        await Assert.That(result.Status).IsEqualTo(HealthStatus.Degraded);
        await Assert.That((((System.Collections.Generic.IEnumerable<string>)(((System.Collections.Generic.IEnumerable<string>)(await Assert.That(result.Data["conditions"]).IsAssignableTo<System.Collections.Generic.IEnumerable<string>>())))))).IsEqualTo(new[] { "persistent_channel_failures" });
    }

    [Test]
    public async Task When_a_draining_consumer_is_at_the_failure_threshold_should_still_report_degraded()
    {
        //Arrange
        await using var host = new MultiSubscriptionReceiveHealthTestHost([new ReceiveHealthSubscription("orders", 1, [3])]);
        host.Dispatcher.Receive();
        await ObservedFailures(host);
        var healthCheck = new ConsumerTakeOutOfServiceHealthCheck(host.Dispatcher, 3);

        //Act
        var drain = host.Dispatcher.End();
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        //Assert
        await Assert.That(drain.IsCompleted).IsFalse();
        await Assert.That((await Assert.That(host.Dispatcher).IsTypeOf<Dispatcher>()).State).IsEqualTo(DispatcherState.DS_RUNNING);
        await Assert.That(result.Status).IsEqualTo(HealthStatus.Degraded);
        await Assert.That((((System.Collections.Generic.IEnumerable<string>)(((System.Collections.Generic.IEnumerable<string>)(await Assert.That(result.Data["conditions"]).IsAssignableTo<System.Collections.Generic.IEnumerable<string>>())))))).IsEqualTo(new[] { "persistent_channel_failures" });
    }

    [Test]
    public async Task When_the_dispatcher_is_awaiting_with_no_consumers_should_report_healthy()
    {
        //Arrange
        await using var host = new MultiSubscriptionReceiveHealthTestHost([new ReceiveHealthSubscription("orders", 1, [3])]);
        var healthCheck = new ConsumerTakeOutOfServiceHealthCheck(host.Dispatcher, 3);

        //Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        //Assert
        await Assert.That((await Assert.That(host.Dispatcher).IsTypeOf<Dispatcher>()).State).IsEqualTo(DispatcherState.DS_AWAITING);
        await Assert.That(host.Dispatcher.Consumers).IsEmpty();
        await Assert.That(result.Status).IsEqualTo(HealthStatus.Healthy);
    }

    private static async Task ObservedFailures(MultiSubscriptionReceiveHealthTestHost host)
    {
        while (host.Transports("orders").Count < 1)
            await Task.Delay(5);
        await Task.WhenAll(host.Transports("orders").Select(transport => transport.FailuresObserved)).WaitAsync(s_guard);
    }
}
