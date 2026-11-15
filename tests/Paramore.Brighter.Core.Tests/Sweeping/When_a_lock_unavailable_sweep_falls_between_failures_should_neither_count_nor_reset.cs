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

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class LockUnavailableSweepBetweenFailuresTests
{
    private const string InvestigateCheck = "brighter-outbox-sweeper-investigate";

    [Test]
    public async Task When_a_lock_unavailable_sweep_falls_between_failures_should_neither_count_nor_reset()
    {
        //Arrange
        var timeProvider = new FakeTimeProvider();
        var mediator = new SwitchableOutboxProducerMediator();
        mediator.Throw("every sweep fails");
        var distributedLock = new SwitchableDistributedLock();
        var services = SweeperContainer.Create(distributedLock, timeProvider, useOutboxSweeper: true,
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
            var firstFailed = WaitForFailedSweeps(mediator, state, 1);
            var afterFirst = (await healthChecks.CheckHealthAsync(CancellationToken.None)).Entries[InvestigateCheck];

            timeProvider.Advance(TimeSpan.FromSeconds(10));
            var secondFailed = WaitForFailedSweeps(mediator, state, 2);
            var afterSecond = (await healthChecks.CheckHealthAsync(CancellationToken.None)).Entries[InvestigateCheck];

            distributedLock.MakeUnavailable();
            timeProvider.Advance(TimeSpan.FromSeconds(10));
            var thirdUnavailable = SpinWait.SpinUntil(
                () => state.Read().LastOutcome == SweepOutcome.LockUnavailable && !state.Read().IsSweepInProgress,
                TimeSpan.FromSeconds(10));
            var afterUnavailable = (await healthChecks.CheckHealthAsync(CancellationToken.None)).Entries[InvestigateCheck];

            distributedLock.Grant();
            timeProvider.Advance(TimeSpan.FromSeconds(10));
            var fourthFailed = WaitForFailedSweeps(mediator, state, 3);
            var afterFourth = (await healthChecks.CheckHealthAsync(CancellationToken.None)).Entries[InvestigateCheck];

            //Assert
            await Assert.That(firstFailed && secondFailed && thirdUnavailable && fourthFailed).IsTrue().Because("The sweeps did not all end");
            await AssertHealthy(afterFirst);
            await AssertHealthy(afterSecond);
            await AssertHealthy(afterUnavailable);

            await Assert.That(afterFourth.Status).IsEqualTo(HealthStatus.Degraded);
            await Assert.That(afterFourth.Status).IsNotEqualTo(HealthStatus.Unhealthy);
            await Assert.That(afterFourth.Data[RemedyCheckConvention.RemedyKindKey]).IsEqualTo(RemedyCheckConvention.Name(RemedyKind.Investigate));
            await Assert.That((await Assert.That(afterFourth.Data[RemedyCheckConvention.ConditionsKey]).IsTypeOf<string[]>())).IsEqualTo(new[] { "consecutive_failed_sweeps" });
            await Assert.That(afterFourth.Description).Contains("3");
        }
        finally
        {
            await sweeper.StopAsync(new CancellationToken(canceled: true));
        }
    }

    private static async Task AssertHealthy(HealthReportEntry entry)
    {
        await Assert.That(entry.Status).IsEqualTo(HealthStatus.Healthy);
        await Assert.That(entry.Status).IsNotEqualTo(HealthStatus.Unhealthy);
        await Assert.That(entry.Data[RemedyCheckConvention.RemedyKindKey]).IsEqualTo(RemedyCheckConvention.Name(RemedyKind.Investigate));
        await Assert.That((await Assert.That(entry.Data[RemedyCheckConvention.ConditionsKey]).IsTypeOf<string[]>())).IsEmpty();
    }

    private static bool WaitForFailedSweeps(SwitchableOutboxProducerMediator mediator, OutboxSweeperState state, int count) =>
        SpinWait.SpinUntil(
            () => mediator.SweepsAttempted >= count && state.Read().LastOutcome == SweepOutcome.Failed && !state.Read().IsSweepInProgress,
            TimeSpan.FromSeconds(10));
}
