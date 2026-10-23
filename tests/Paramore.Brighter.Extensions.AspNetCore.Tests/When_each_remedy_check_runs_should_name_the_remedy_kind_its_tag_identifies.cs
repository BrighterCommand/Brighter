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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;
using Paramore.Brighter.Outbox.Hosting.HealthChecks;
using Paramore.Brighter.ServiceActivator.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace Paramore.Brighter.Extensions.AspNetCore.Tests;

/// <summary>
/// Characterises that each remedy check names, in its <c>Data</c>, the remedy kind its tag identifies, whether Healthy or Degraded.
/// </summary>
[Collection(DispatcherHealthCheckCollection.NAME)]
public class RemedyKindDataTests
{
    private static readonly TimeSpan s_guard = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task When_each_remedy_check_runs_should_name_the_remedy_kind_its_tag_identifies()
    {
        //Arrange
        var healthy = new List<(RemedyKind Kind, IHealthCheck Check)>();
        var degraded = new List<(RemedyKind Kind, IHealthCheck Check)>();

        await using var idleHost = new MultiSubscriptionReceiveHealthTestHost([new ReceiveHealthSubscription("orders", 2)]);
        healthy.Add((RemedyKind.Restart, new ConsumerRestartHealthCheck(idleHost.Dispatcher)));
        healthy.Add((RemedyKind.TakeOutOfService, new ConsumerTakeOutOfServiceHealthCheck(idleHost.Dispatcher, 3)));

        await using var missingHost = new MultiSubscriptionReceiveHealthTestHost([new ReceiveHealthSubscription("orders", 2)]);
        missingHost.Dispatcher.Receive();
        await ConsumersRunning(missingHost, 2);
        missingHost.StopPerformer("orders", 1);
        await ConsumersRunning(missingHost, 1);
        degraded.Add((RemedyKind.Restart, new ConsumerRestartHealthCheck(missingHost.Dispatcher)));

        await using var failingHost = new MultiSubscriptionReceiveHealthTestHost([new ReceiveHealthSubscription("orders", 1, [3])]);
        failingHost.Dispatcher.Receive();
        await FailuresObserved(failingHost, 1);
        degraded.Add((RemedyKind.TakeOutOfService, new ConsumerTakeOutOfServiceHealthCheck(failingHost.Dispatcher, 3)));

        var activeServices = new ServiceCollection();
        OutboxSweeperRecordScenario.RegisterActiveSweeper(activeServices, new FakeTimeProvider());
        using var activeProvider = activeServices.BuildServiceProvider();
        healthy.Add((RemedyKind.Restart, SweeperRestart(activeProvider)));
        healthy.Add((RemedyKind.Investigate, SweeperInvestigate(activeProvider)));

        var hungServices = new ServiceCollection();
        OutboxSweeperRecordScenario.RegisterHungSweep(hungServices, new FakeTimeProvider());
        using var hungProvider = hungServices.BuildServiceProvider();
        degraded.Add((RemedyKind.Restart, SweeperRestart(hungProvider)));

        var failedServices = new ServiceCollection();
        OutboxSweeperRecordScenario.RegisterConsecutiveFailures(failedServices, new FakeTimeProvider());
        using var failedProvider = failedServices.BuildServiceProvider();
        degraded.Add((RemedyKind.Investigate, SweeperInvestigate(failedProvider)));

        //Act
        var healthyResults = await RunAll(healthy);
        var degradedResults = await RunAll(degraded);

        //Assert
        Assert.All(healthyResults, entry => Assert.Equal(HealthStatus.Healthy, entry.Result.Status));
        Assert.All(degradedResults, entry => Assert.Equal(HealthStatus.Degraded, entry.Result.Status));
        Assert.All(healthyResults.Concat(degradedResults), entry =>
        {
            Assert.Equal(RemedyCheckConvention.Name(entry.Kind), entry.Result.Data[RemedyCheckConvention.RemedyKindKey]);
            Assert.True(entry.Result.Data.ContainsKey(RemedyCheckConvention.ConditionsKey));
        });
    }

    private static OutboxSweeperRestartHealthCheck SweeperRestart(IServiceProvider provider)
        => new(provider.GetRequiredService<OutboxSweeperState>(), provider.GetRequiredService<TimedOutboxSweeperOptions>(), TimeSpan.FromSeconds(30));

    private static OutboxSweeperInvestigateHealthCheck SweeperInvestigate(IServiceProvider provider)
        => new(provider.GetRequiredService<OutboxSweeperState>(), provider.GetRequiredService<TimedOutboxSweeperOptions>());

    private static async Task<(RemedyKind Kind, HealthCheckResult Result)[]> RunAll(IEnumerable<(RemedyKind Kind, IHealthCheck Check)> checks)
    {
        var results = new List<(RemedyKind Kind, HealthCheckResult Result)>();
        foreach (var (kind, check) in checks)
            results.Add((kind, await check.CheckHealthAsync(new HealthCheckContext())));
        return results.ToArray();
    }

    private static async Task ConsumersRunning(MultiSubscriptionReceiveHealthTestHost host, int expected)
    {
        using var timeout = new CancellationTokenSource(s_guard);
        while (host.Dispatcher.Consumers.Count() != expected)
            await Task.Delay(5, timeout.Token);
    }

    private static async Task FailuresObserved(MultiSubscriptionReceiveHealthTestHost host, int expected)
    {
        using var timeout = new CancellationTokenSource(s_guard);
        while (host.Transports("orders").Count < expected)
            await Task.Delay(5, timeout.Token);
        await Task.WhenAll(host.Transports("orders").Select(transport => transport.FailuresObserved)).WaitAsync(s_guard);
    }
}
