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

public class StopAbandonsBlockedSweepTests
{
    private const string RestartCheck = "brighter-outbox-sweeper-restart";

    [Fact]
    public async Task When_stop_abandons_a_blocked_sweep_should_keep_growing_its_running_time()
    {
        //Arrange
        var timeProvider = new FakeTimeProvider();
        using var mediator = new GatedOutboxProducerMediator();
        var services = SweeperContainer.Create(new InMemoryLock(), timeProvider, useOutboxSweeper: true,
            configureSweeper: options => options.TimerInterval = 10);
        services.AddSingleton<IAmAnOutboxProducerMediator>(mediator);
        services.AddLogging();
        services.AddMetrics();
        services.AddHealthChecks().AddBrighterOutboxSweeperHealthChecks();
        services.AddOpenTelemetry().WithMetrics(metrics => metrics.AddBrighterInstrumentation());
        using var provider = services.BuildServiceProvider();
        using var meterProvider = provider.GetRequiredService<MeterProvider>();
        using var runningTime = new RunningTimeListener(provider.GetRequiredService<IMeterFactory>());
        var healthChecks = provider.GetRequiredService<HealthCheckService>();
        var sweeper = provider.GetServices<IHostedService>().OfType<TimedOutboxSweeper>().Single();

        try
        {
            await sweeper.StartAsync(CancellationToken.None);
            var reachedMediator = mediator.WaitForSweepToStart(TimeSpan.FromSeconds(10));
            timeProvider.Advance(TimeSpan.FromSeconds(25));

            //Act
            await sweeper.StopAsync(new CancellationToken(canceled: true));
            timeProvider.Advance(TimeSpan.FromSeconds(31));
            runningTime.Observe();
            var report = await healthChecks.CheckHealthAsync(CancellationToken.None);

            //Assert
            Assert.True(reachedMediator, "The sweep did not reach the mediator");
            Assert.Equal([56.0], runningTime.Values);

            var restart = report.Entries[RestartCheck];
            Assert.Equal(HealthStatus.Degraded, restart.Status);
            Assert.NotEqual(HealthStatus.Unhealthy, restart.Status);
            Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Restart), restart.Data[RemedyCheckConvention.RemedyKindKey]);
            Assert.Equal(new[] { "hung_sweep" }, Assert.IsType<string[]>(restart.Data[RemedyCheckConvention.ConditionsKey]));
            Assert.Contains("56", restart.Description);
        }
        finally
        {
            mediator.Open();
            sweeper.Dispose();
        }
    }
}
