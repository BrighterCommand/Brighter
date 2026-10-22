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
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

[Collection(DispatcherHealthCheckCollection.NAME)]
public class ConsumerTakeOutOfServiceThresholdTests
{
    private static readonly TimeSpan s_guard = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task When_the_threshold_is_one_should_degrade_on_a_single_failure()
    {
        //Arrange
        await using var host = StartedHost([0, 1], out var ready);
        await ready.WaitAsync(s_guard);
        var healthCheck = new ConsumerTakeOutOfServiceHealthCheck(host.Dispatcher, 1);

        //Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        //Assert
        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void When_the_threshold_is_not_positive_should_throw_from_the_constructor(int threshold)
    {
        //Arrange
        //Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new ConsumerTakeOutOfServiceHealthCheck(null, threshold));

        //Assert
        Assert.Equal("channelFailureThreshold", exception.ParamName);
    }

    [Fact]
    public async Task When_no_threshold_is_given_should_default_to_three_failures()
    {
        //Arrange
        await using var below = StartedHost([0, 2], out var belowReady);
        await using var at = StartedHost([0, 3], out var atReady);
        await belowReady.WaitAsync(s_guard);
        await atReady.WaitAsync(s_guard);
        var belowCheck = new ConsumerTakeOutOfServiceHealthCheck(below.Dispatcher);
        var atCheck = new ConsumerTakeOutOfServiceHealthCheck(at.Dispatcher);

        //Act
        var belowResult = await belowCheck.CheckHealthAsync(new HealthCheckContext());
        var atResult = await atCheck.CheckHealthAsync(new HealthCheckContext());

        //Assert
        Assert.Equal(HealthStatus.Healthy, belowResult.Status);
        Assert.Equal(HealthStatus.Degraded, atResult.Status);
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
