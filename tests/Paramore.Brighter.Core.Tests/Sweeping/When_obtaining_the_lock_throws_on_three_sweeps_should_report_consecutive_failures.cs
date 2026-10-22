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

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class ThrowingLockFailureThresholdTests
{
    private const string InvestigateCheck = "brighter-outbox-sweeper-investigate";

    [Fact]
    public async Task When_obtaining_the_lock_throws_on_three_sweeps_should_report_consecutive_failures()
    {
        //Arrange
        var timeProvider = new FakeTimeProvider();
        var distributedLock = new ThrowingDistributedLock("the lock store is unreachable");
        var services = SweeperContainer.Create(distributedLock, timeProvider, useOutboxSweeper: true,
            configureSweeper: options => options.TimerInterval = 10);
        services.AddLogging();
        services.AddHealthChecks().AddBrighterOutboxSweeperHealthChecks();
        using var provider = services.BuildServiceProvider();
        var state = provider.GetRequiredService<OutboxSweeperState>();
        var healthChecks = provider.GetRequiredService<HealthCheckService>();
        var sweeper = provider.GetServices<IHostedService>().OfType<TimedOutboxSweeper>().Single();

        try
        {
            //Act
            await sweeper.StartAsync(CancellationToken.None);
            var firstFailed = WaitForFailedSweep(distributedLock, state, 1);
            timeProvider.Advance(TimeSpan.FromSeconds(10));
            var secondFailed = WaitForFailedSweep(distributedLock, state, 2);
            timeProvider.Advance(TimeSpan.FromSeconds(10));
            var thirdFailed = WaitForFailedSweep(distributedLock, state, 3);
            var failedCount = state.Read().ConsecutiveFailedSweeps;
            var report = (await healthChecks.CheckHealthAsync(CancellationToken.None)).Entries[InvestigateCheck];

            //Assert
            Assert.True(firstFailed && secondFailed && thirdFailed, "The sweeps did not all fail");
            Assert.Equal(3, failedCount);
            Assert.Equal(HealthStatus.Degraded, report.Status);
            Assert.NotEqual(HealthStatus.Unhealthy, report.Status);
            Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Investigate), report.Data[RemedyCheckConvention.RemedyKindKey]);
            Assert.Equal(new[] { "consecutive_failed_sweeps" }, Assert.IsType<string[]>(report.Data[RemedyCheckConvention.ConditionsKey]));
            Assert.Contains("3", report.Description);
        }
        finally
        {
            await sweeper.StopAsync(new CancellationToken(canceled: true));
        }
    }

    private static bool WaitForFailedSweep(ThrowingDistributedLock distributedLock, OutboxSweeperState state, int sweeps) =>
        SpinWait.SpinUntil(
            () => distributedLock.Attempts >= sweeps
                  && state.Read().LastOutcome == SweepOutcome.Failed
                  && !state.Read().IsSweepInProgress,
            TimeSpan.FromSeconds(10));
}
