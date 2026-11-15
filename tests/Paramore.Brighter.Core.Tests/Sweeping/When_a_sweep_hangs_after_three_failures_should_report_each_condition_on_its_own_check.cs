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

public class HungSweepAfterFailuresTests
{
    private const string RestartCheck = "brighter-outbox-sweeper-restart";
    private const string InvestigateCheck = "brighter-outbox-sweeper-investigate";

    [Test]
    public async Task When_a_sweep_hangs_after_three_failures_should_report_each_condition_on_its_own_check()
    {
        //Arrange
        var timeProvider = new FakeTimeProvider();
        var mediator = new SwitchableOutboxProducerMediator();
        mediator.Throw("every sweep fails");
        using var lockGate = new GatedDistributedLock().PassFirst(3);
        var services = SweeperContainer.Create(lockGate, timeProvider, useOutboxSweeper: true,
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
            var firstFailed = WaitForSweeps(mediator, state, 1);
            timeProvider.Advance(TimeSpan.FromSeconds(10));
            var secondFailed = WaitForSweeps(mediator, state, 2);
            timeProvider.Advance(TimeSpan.FromSeconds(10));
            var thirdFailed = WaitForSweeps(mediator, state, 3);
            timeProvider.Advance(TimeSpan.FromSeconds(10));
            var reachedLock = lockGate.WaitForSweepToStart(TimeSpan.FromSeconds(10));
            timeProvider.Advance(TimeSpan.FromSeconds(31));
            var report = await healthChecks.CheckHealthAsync(CancellationToken.None);

            //Assert
            await Assert.That(firstFailed && secondFailed && thirdFailed).IsTrue().Because("The failing sweeps did not all end");
            await Assert.That(reachedLock).IsTrue().Because("The fourth sweep did not reach the lock");

            var restart = report.Entries[RestartCheck];
            await Assert.That(restart.Status).IsEqualTo(HealthStatus.Degraded);
            await Assert.That(restart.Status).IsNotEqualTo(HealthStatus.Unhealthy);
            await Assert.That(restart.Data[RemedyCheckConvention.RemedyKindKey]).IsEqualTo(RemedyCheckConvention.Name(RemedyKind.Restart));
            await Assert.That((await Assert.That(restart.Data[RemedyCheckConvention.ConditionsKey]).IsTypeOf<string[]>())).IsEqualTo(new[] { "hung_sweep" });
            await Assert.That(restart.Description).Contains("31");
            await Assert.That(restart.Description).Contains("30");
            await Assert.That(restart.Description).Contains("10");
            await Assert.That(restart.Description).DoesNotContain("consecutive");
            await Assert.That(restart.Data.ContainsKey("consecutive_failed_sweeps")).IsFalse();

            var investigate = report.Entries[InvestigateCheck];
            await Assert.That(investigate.Status).IsEqualTo(HealthStatus.Degraded);
            await Assert.That(investigate.Status).IsNotEqualTo(HealthStatus.Unhealthy);
            await Assert.That(investigate.Data[RemedyCheckConvention.RemedyKindKey]).IsEqualTo(RemedyCheckConvention.Name(RemedyKind.Investigate));
            await Assert.That((await Assert.That(investigate.Data[RemedyCheckConvention.ConditionsKey]).IsTypeOf<string[]>())).IsEqualTo(new[] { "consecutive_failed_sweeps" });
            await Assert.That(investigate.Description).Contains("3");
            await Assert.That(investigate.Description).Contains("10");
            await Assert.That(investigate.Description).DoesNotContain("running for");
            await Assert.That(investigate.Data.ContainsKey("hung_sweep")).IsFalse();
        }
        finally
        {
            lockGate.Open();
            await sweeper.StopAsync(new CancellationToken(canceled: true));
        }
    }

    private static bool WaitForSweeps(SwitchableOutboxProducerMediator mediator, OutboxSweeperState state, int count) =>
        SpinWait.SpinUntil(() => mediator.SweepsAttempted >= count && !state.Read().IsSweepInProgress, TimeSpan.FromSeconds(10));
}
