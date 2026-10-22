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
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;
using Paramore.Brighter.Extensions.Diagnostics;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class SweeperRegisteredByUseOutboxSweeperTests
{
    private const string RunningTime = "paramore.brighter.outbox_sweeper.sweep.running_time";

    [Fact]
    public async Task When_the_sweeper_is_registered_with_use_outbox_sweeper_should_report_its_hung_sweep()
    {
        //Arrange
        var timeProvider = new FakeTimeProvider();
        using var lockGate = new GatedDistributedLock();
        var services = SweeperContainer.Create(lockGate, timeProvider, useOutboxSweeper: true,
            configureSweeper: options => options.TimerInterval = 10);
        services.AddLogging();
        services.AddMetrics();
        services.AddHealthChecks().AddBrighterOutboxSweeperHealthChecks();
        services.AddOpenTelemetry().WithMetrics(metrics => metrics.AddBrighterInstrumentation());
        using var provider = services.BuildServiceProvider();
        var meterFactory = provider.GetRequiredService<IMeterFactory>();
        var runningTimes = new List<double>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (!ReferenceEquals(instrument.Meter.Scope, meterFactory)) return;
            if (instrument.Name != RunningTime) return;
            meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<double>((_, value, _, _) => runningTimes.Add(value));
        var sweeper = provider.GetServices<IHostedService>().OfType<TimedOutboxSweeper>().Single();

        try
        {
            await sweeper.StartAsync(CancellationToken.None);
            var reachedLock = lockGate.WaitForSweepToStart(TimeSpan.FromSeconds(10));

            //Act
            timeProvider.Advance(TimeSpan.FromSeconds(31));
            using var meterProvider = provider.GetRequiredService<MeterProvider>();
            listener.Start();
            listener.RecordObservableInstruments();
            var report = await provider.GetRequiredService<HealthCheckService>().CheckHealthAsync(CancellationToken.None);

            //Assert
            Assert.True(reachedLock, "The sweep did not reach the lock");
            var restart = report.Entries["brighter-outbox-sweeper-restart"];
            Assert.Equal(HealthStatus.Degraded, restart.Status);
            Assert.NotEqual(HealthStatus.Unhealthy, restart.Status);
            Assert.DoesNotContain("no outbox sweeper is registered", restart.Description, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Restart), restart.Data[RemedyCheckConvention.RemedyKindKey]);
            var conditions = Assert.IsType<string[]>(restart.Data[RemedyCheckConvention.ConditionsKey]);
            Assert.Equal(new[] { "hung_sweep" }, conditions);
            Assert.Equal([31.0], runningTimes);
        }
        finally
        {
            lockGate.Open();
            await sweeper.StopAsync(new CancellationToken(canceled: true));
        }
    }
}
