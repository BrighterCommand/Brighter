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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;
using Paramore.Brighter.Extensions.Diagnostics;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class HungSweepReleasedTests
{
    private const string RestartCheck = "brighter-outbox-sweeper-restart";

    [Fact]
    public async Task When_a_hung_sweep_is_released_and_completes_should_report_healthy_again()
    {
        //Arrange
        var timeProvider = new FakeTimeProvider();
        using var lockGate = new GatedDistributedLock();
        using var mediator = new GatedOutboxProducerMediator();
        mediator.Open();
        var services = SweeperContainer.Create(lockGate, timeProvider, useOutboxSweeper: true,
            configureSweeper: options => options.TimerInterval = 10);
        services.AddSingleton<IAmAnOutboxProducerMediator>(mediator);
        services.AddLogging();
        services.AddHealthChecks().AddBrighterOutboxSweeperHealthChecks();
        using var provider = services.BuildServiceProvider();
        var healthChecks = provider.GetRequiredService<HealthCheckService>();
        var sweeper = provider.GetServices<IHostedService>().OfType<TimedOutboxSweeper>().Single();

        try
        {
            await sweeper.StartAsync(CancellationToken.None);
            var reachedLock = lockGate.WaitForSweepToStart(TimeSpan.FromSeconds(10));
            timeProvider.Advance(TimeSpan.FromSeconds(31));
            var hungReport = await healthChecks.CheckHealthAsync(CancellationToken.None);

            //Act
            var stopping = sweeper.StopAsync(CancellationToken.None);
            lockGate.Open();
            await stopping.WaitAsync(TimeSpan.FromSeconds(10));
            timeProvider.Advance(TimeSpan.FromSeconds(31));
            var releasedReport = await healthChecks.CheckHealthAsync(CancellationToken.None);

            //Assert
            Assert.True(reachedLock, "The sweep did not reach the lock");
            var hung = hungReport.Entries[RestartCheck];
            Assert.Equal(HealthStatus.Degraded, hung.Status);
            Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Restart), hung.Data[RemedyCheckConvention.RemedyKindKey]);
            var restart = releasedReport.Entries[RestartCheck];
            Assert.Equal(HealthStatus.Healthy, restart.Status);
            Assert.NotEqual(HealthStatus.Unhealthy, restart.Status);
            Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Restart), restart.Data[RemedyCheckConvention.RemedyKindKey]);
            Assert.Empty(Assert.IsType<string[]>(restart.Data[RemedyCheckConvention.ConditionsKey]));
        }
        finally
        {
            lockGate.Open();
            await sweeper.StopAsync(new CancellationToken(canceled: true));
        }
    }
}
