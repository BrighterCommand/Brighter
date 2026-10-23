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
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using Paramore.Brighter.Core.Tests.Observability.Metrics;
using Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;
using Paramore.Brighter.Extensions.Diagnostics;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class SweepersSharingALockHolderHungTests
{
    private const string RestartCheck = "brighter-outbox-sweeper-restart";

    [Fact]
    public async Task When_two_sweepers_share_a_lock_and_the_holder_hangs_should_report_only_the_holder_hung()
    {
        //Arrange
        var timeProvider = new FakeTimeProvider();
        var sharedLock = new InMemoryLock();
        var waitingLock = new CountingDistributedLock(sharedLock);
        using var holderMediator = new GatedOutboxProducerMediator();
        using var waiterMediator = new GatedOutboxProducerMediator();
        using var holder = new SweeperHost(sharedLock, holderMediator, timeProvider);
        using var waiter = new SweeperHost(waitingLock, waiterMediator, timeProvider);

        try
        {
            //Act
            await holder.Sweeper.StartAsync(CancellationToken.None);
            var holderReachedMediator = holderMediator.WaitForSweepToStart(TimeSpan.FromSeconds(10));
            await waiter.Sweeper.StartAsync(CancellationToken.None);
            var waiterTriedOnce = SpinWait.SpinUntil(() => waitingLock.Attempts >= 1, TimeSpan.FromSeconds(10));

            timeProvider.Advance(TimeSpan.FromSeconds(31));
            var waiterTriedTwice = SpinWait.SpinUntil(
                () => waitingLock.Attempts >= 2 && !waiter.State.Read().IsSweepInProgress, TimeSpan.FromSeconds(10));
            var holderReport = (await holder.HealthChecks.CheckHealthAsync(CancellationToken.None)).Entries[RestartCheck];
            holder.RunningTime.Observe();
            waiter.RunningTime.Observe();
            var holderGauge = holder.RunningTime.Values.Last();
            var maxGauge = Math.Max(holderGauge, waiter.RunningTime.Values.Last());

            timeProvider.Advance(TimeSpan.FromSeconds(5));
            var waiterReport = (await waiter.HealthChecks.CheckHealthAsync(CancellationToken.None)).Entries[RestartCheck];
            waiter.RunningTime.Observe();
            var waiterGauge = waiter.RunningTime.Values.Last();

            //Assert
            Assert.Equal(HealthStatus.Degraded, holderReport.Status);
            Assert.NotEqual(HealthStatus.Unhealthy, holderReport.Status);
            Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Restart), holderReport.Data[RemedyCheckConvention.RemedyKindKey]);
            Assert.Equal(new[] { "hung_sweep" }, Assert.IsType<string[]>(holderReport.Data[RemedyCheckConvention.ConditionsKey]));
            Assert.Equal(31.0, holderGauge);
            Assert.Equal(31.0, maxGauge);

            Assert.Equal(HealthStatus.Healthy, waiterReport.Status);
            Assert.NotEqual(HealthStatus.Unhealthy, waiterReport.Status);
            Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Restart), waiterReport.Data[RemedyCheckConvention.RemedyKindKey]);
            Assert.Empty(Assert.IsType<string[]>(waiterReport.Data[RemedyCheckConvention.ConditionsKey]));
            Assert.Equal(0.0, waiterGauge);

            Assert.True(holderReachedMediator, "The holder's sweep did not reach the mediator");
            Assert.True(waiterTriedOnce, "The waiting sweeper did not try for the lock at the start");
            Assert.True(waiterTriedTwice, "The waiting sweeper's second lock-unavailable sweep did not end");
        }
        finally
        {
            await waiter.Sweeper.StopAsync(new CancellationToken(canceled: true));
            await holder.Sweeper.StopAsync(new CancellationToken(canceled: true));
            holderMediator.Open();
        }
    }

    private sealed class SweeperHost : IDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly MeterProvider _meterProvider;

        public SweeperHost(IDistributedLock distributedLock, IAmAnOutboxProducerMediator mediator, TimeProvider timeProvider)
        {
            var services = SweeperContainer.Create(distributedLock, timeProvider, useOutboxSweeper: true,
                configureSweeper: options => options.TimerInterval = 10);
            services.AddSingleton(mediator);
            services.AddLogging();
            services.AddMetrics();
            services.AddHealthChecks().AddBrighterOutboxSweeperHealthChecks();
            services.AddOpenTelemetry().WithMetrics(metrics => metrics.AddBrighterInstrumentation());
            _provider = services.BuildServiceProvider();
            _meterProvider = _provider.GetRequiredService<MeterProvider>();
            RunningTime = new RunningTimeListener(_provider.GetRequiredService<IMeterFactory>());
            State = _provider.GetRequiredService<OutboxSweeperState>();
            HealthChecks = _provider.GetRequiredService<HealthCheckService>();
            Sweeper = _provider.GetServices<IHostedService>().OfType<TimedOutboxSweeper>().Single();
        }

        public TimedOutboxSweeper Sweeper { get; }
        public OutboxSweeperState State { get; }
        public HealthCheckService HealthChecks { get; }
        public RunningTimeListener RunningTime { get; }

        public void Dispose()
        {
            RunningTime.Dispose();
            _meterProvider.Dispose();
            _provider.Dispose();
        }
    }
}
