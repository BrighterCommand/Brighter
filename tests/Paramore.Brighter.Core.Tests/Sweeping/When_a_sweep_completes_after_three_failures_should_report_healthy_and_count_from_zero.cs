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

public class CompletedSweepAfterThreeFailuresTests
{
    private const string InvestigateCheck = "brighter-outbox-sweeper-investigate";

    [Fact]
    public async Task When_a_sweep_completes_after_three_failures_should_report_healthy_and_count_from_zero()
    {
        //Arrange
        var timeProvider = new FakeTimeProvider();
        var mediator = new SwitchableOutboxProducerMediator();
        mediator.Throw("every sweep fails");
        var services = SweeperContainer.Create(new InMemoryLock(), timeProvider, useOutboxSweeper: true,
            configureSweeper: options => options.TimerInterval = 10);
        services.AddSingleton<IAmAnOutboxProducerMediator>(mediator);
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
            var firstFailed = WaitForSweep(mediator, state, 1, SweepOutcome.Failed);
            AdvanceOneInterval(timeProvider);
            var secondFailed = WaitForSweep(mediator, state, 2, SweepOutcome.Failed);
            AdvanceOneInterval(timeProvider);
            var thirdFailed = WaitForSweep(mediator, state, 3, SweepOutcome.Failed);
            var afterThreeFailures = (await healthChecks.CheckHealthAsync(CancellationToken.None)).Entries[InvestigateCheck];

            mediator.Complete();
            AdvanceOneInterval(timeProvider);
            var completed = WaitForSweep(mediator, state, 4, SweepOutcome.Completed);
            var afterCompleted = (await healthChecks.CheckHealthAsync(CancellationToken.None)).Entries[InvestigateCheck];

            mediator.Throw("fails again");
            AdvanceOneInterval(timeProvider);
            var failedAgain = WaitForSweep(mediator, state, 5, SweepOutcome.Failed);
            var failedCount = state.Read().ConsecutiveFailedSweeps;
            var afterFailedAgain = (await healthChecks.CheckHealthAsync(CancellationToken.None)).Entries[InvestigateCheck];

            //Assert
            Assert.True(firstFailed && secondFailed && thirdFailed && completed && failedAgain, "The sweeps did not all end");
            Assert.Equal(HealthStatus.Degraded, afterThreeFailures.Status);
            Assert.NotEqual(HealthStatus.Unhealthy, afterThreeFailures.Status);
            Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Investigate), afterThreeFailures.Data[RemedyCheckConvention.RemedyKindKey]);
            Assert.Equal(new[] { "consecutive_failed_sweeps" }, Assert.IsType<string[]>(afterThreeFailures.Data[RemedyCheckConvention.ConditionsKey]));

            AssertHealthy(afterCompleted);
            AssertHealthy(afterFailedAgain);
            Assert.Equal(1, failedCount);
        }
        finally
        {
            await sweeper.StopAsync(new CancellationToken(canceled: true));
        }
    }

    private static void AssertHealthy(HealthReportEntry entry)
    {
        Assert.Equal(HealthStatus.Healthy, entry.Status);
        Assert.NotEqual(HealthStatus.Unhealthy, entry.Status);
        Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Investigate), entry.Data[RemedyCheckConvention.RemedyKindKey]);
        Assert.Empty(Assert.IsType<string[]>(entry.Data[RemedyCheckConvention.ConditionsKey]));
    }

    private static void AdvanceOneInterval(FakeTimeProvider timeProvider) => timeProvider.Advance(TimeSpan.FromSeconds(10));

    private static bool WaitForSweep(SwitchableOutboxProducerMediator mediator, OutboxSweeperState state, int count, SweepOutcome outcome) =>
        SpinWait.SpinUntil(
            () => mediator.SweepsAttempted >= count && state.Read().LastOutcome == outcome && !state.Read().IsSweepInProgress,
            TimeSpan.FromSeconds(10));
}
