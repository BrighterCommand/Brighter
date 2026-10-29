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

public class ThrowingMeterHealthyChecksTests
{
    private const string RestartCheck = "brighter-outbox-sweeper-restart";
    private const string InvestigateCheck = "brighter-outbox-sweeper-investigate";

    [Fact]
    public async Task When_the_sweeper_meter_throws_should_complete_sweeps_and_keep_both_checks_healthy()
    {
        //Arrange
        var marker = $"meter-broken-{Guid.NewGuid():N}";
        var timeProvider = new FakeTimeProvider();
        var mediator = new SwitchableOutboxProducerMediator();
        mediator.Complete();
        var services = SweeperContainer.Create(new InMemoryLock(), timeProvider, useOutboxSweeper: true,
            configureSweeper: options => options.TimerInterval = 10);
        services.AddSingleton<IAmAnOutboxProducerMediator>(mediator);
        services.AddSingleton<IAmABrighterSweeperMeter>(new ThrowingSweeperMeter(marker));
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
            var firstRecorded = WaitForFailedRecordings(marker, 2);
            timeProvider.Advance(TimeSpan.FromSeconds(10));
            var secondRecorded = WaitForFailedRecordings(marker, 4);
            timeProvider.Advance(TimeSpan.FromSeconds(10));
            var thirdRecorded = WaitForFailedRecordings(marker, 6);

            await sweeper.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));
            timeProvider.Advance(TimeSpan.FromHours(1));
            var report = await healthChecks.CheckHealthAsync(CancellationToken.None);

            //Assert
            Assert.True(firstRecorded && secondRecorded && thirdRecorded, "The failed recordings were not all logged");
            Assert.Equal(3, mediator.SweepsAttempted);
            var logged = CapturingLogSink.Instance.Find(typeof(TimedOutboxSweeper).FullName!, marker);
            Assert.Equal(6, logged.Count);
            Assert.All(logged, e => Assert.Equal(Serilog.Events.LogEventLevel.Warning, e.Level));

            var restart = report.Entries[RestartCheck];
            Assert.Equal(HealthStatus.Healthy, restart.Status);
            Assert.NotEqual(HealthStatus.Unhealthy, restart.Status);
            Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Restart), restart.Data[RemedyCheckConvention.RemedyKindKey]);

            var investigate = report.Entries[InvestigateCheck];
            Assert.Equal(HealthStatus.Healthy, investigate.Status);
            Assert.NotEqual(HealthStatus.Unhealthy, investigate.Status);
            Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Investigate), investigate.Data[RemedyCheckConvention.RemedyKindKey]);

            Assert.False(state.Read().IsSweepInProgress);
            Assert.Equal(SweepOutcome.Completed, state.Read().LastOutcome);
        }
        finally
        {
            await sweeper.StopAsync(new CancellationToken(canceled: true));
        }
    }

    private static bool WaitForFailedRecordings(string marker, int count) =>
        SpinWait.SpinUntil(
            () => CapturingLogSink.Instance.Find(typeof(TimedOutboxSweeper).FullName!, marker).Count >= count,
            TimeSpan.FromSeconds(10));
}
