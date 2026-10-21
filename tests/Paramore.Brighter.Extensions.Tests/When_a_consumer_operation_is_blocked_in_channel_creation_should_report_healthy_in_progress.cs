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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.ServiceActivator.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class ConsumerRestartOperationInProgressTests
{
    private static readonly TimeSpan s_guard = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task When_a_consumer_operation_is_blocked_in_channel_creation_should_report_healthy_in_progress()
    {
        //Arrange
        InMemoryGatedReceiveHealthChannelFactory? gate = null;
        await using var host = new MultiSubscriptionReceiveHealthTestHost([new ReceiveHealthSubscription("orders", 2)],
            decorateChannelFactory: factory => gate = new InMemoryGatedReceiveHealthChannelFactory(factory));
        var healthCheck = new ConsumerRestartHealthCheck(host.Dispatcher);

        try
        {
            //Act
            var receive = Task.Run(() => host.Dispatcher.Receive());
            await gate!.CreationBlocked.Task.WaitAsync(s_guard);
            var inProgress = await healthCheck.CheckHealthAsync(new HealthCheckContext());
            gate.Release();
            await receive.WaitAsync(s_guard);
            await ConsumersRunning(host, 2);
            host.StopPerformer("orders", 1);
            await ConsumersRunning(host, 1);
            var completed = await healthCheck.CheckHealthAsync(new HealthCheckContext());

            //Assert
            Assert.Equal(HealthStatus.Healthy, inProgress.Status);
            Assert.Contains("consumer operation is in progress", inProgress.Description);
            Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Restart), inProgress.Data["remedy_kind"]);
            Assert.Empty(Assert.IsAssignableFrom<IEnumerable<string>>(inProgress.Data["conditions"]));
            Assert.Equal(1, inProgress.Data["consumer_operations_in_flight"]);
            Assert.Equal(HealthStatus.Degraded, completed.Status);
            Assert.Contains("orders has 1 of 2 expected consumers", completed.Description);
            Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Restart), completed.Data["remedy_kind"]);
            Assert.Equal(0, completed.Data["consumer_operations_in_flight"]);
        }
        finally
        {
            gate?.Release();
        }
    }

    [Fact]
    public async Task When_a_second_consumer_operation_is_blocked_in_channel_creation_should_report_healthy_in_progress()
    {
        //Arrange
        InMemoryGatedReceiveHealthChannelFactory? gate = null;
        await using var host = new MultiSubscriptionReceiveHealthTestHost([new ReceiveHealthSubscription("orders", 2)],
            decorateChannelFactory: factory => gate = new InMemoryGatedReceiveHealthChannelFactory(factory));
        var healthCheck = new ConsumerRestartHealthCheck(host.Dispatcher);

        try
        {
            var receive = Task.Run(() => host.Dispatcher.Receive());
            await gate!.CreationBlocked.Task.WaitAsync(s_guard);
            gate.Release();
            await receive.WaitAsync(s_guard);
            await ConsumersRunning(host, 2);
            gate.Reset();

            //Act
            var scaleUp = Task.Run(() => ((Dispatcher)host.Dispatcher).SetActivePerformers("orders", 3));
            await gate.CreationBlocked.Task.WaitAsync(s_guard);
            var inProgress = await healthCheck.CheckHealthAsync(new HealthCheckContext());
            gate.Release();
            await scaleUp.WaitAsync(s_guard);

            //Assert
            Assert.Equal(HealthStatus.Healthy, inProgress.Status);
            Assert.Contains("consumer operation is in progress", inProgress.Description);
            Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Restart), inProgress.Data["remedy_kind"]);
            Assert.Equal(1, inProgress.Data["consumer_operations_in_flight"]);
        }
        finally
        {
            gate?.Release();
        }
    }

    private static async Task ConsumersRunning(MultiSubscriptionReceiveHealthTestHost host, int expected)
    {
        using var timeout = new CancellationTokenSource(s_guard);
        while (host.Dispatcher.Consumers.Count() != expected)
            await Task.Delay(5, timeout.Token);
    }
}
