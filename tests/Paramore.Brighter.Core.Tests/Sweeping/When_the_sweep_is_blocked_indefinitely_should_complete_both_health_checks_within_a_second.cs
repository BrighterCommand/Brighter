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

public class BlockedSweepHealthCheckLatencyTests
{
    private const string RestartCheck = "brighter-outbox-sweeper-restart";
    private const string InvestigateCheck = "brighter-outbox-sweeper-investigate";

    [Theory]
    [InlineData(RestartCheck, RemedyKind.Restart)]
    [InlineData(InvestigateCheck, RemedyKind.Investigate)]
    public async Task When_the_sweep_is_blocked_indefinitely_should_complete_both_health_checks_within_a_second(
        string checkName, RemedyKind remedyKind)
    {
        //Arrange
        var timeProvider = new FakeTimeProvider();
        using var mediator = new GatedOutboxProducerMediator();
        var services = SweeperContainer.Create(new InMemoryLock(), timeProvider, useOutboxSweeper: true,
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
            var reachedMediator = mediator.WaitForSweepToStart(TimeSpan.FromSeconds(10));
            timeProvider.Advance(TimeSpan.FromSeconds(5));

            //Act
            var report = await Task.Run(
                    () => healthChecks.CheckHealthAsync(registration => registration.Name == checkName, CancellationToken.None))
                .WaitAsync(TimeSpan.FromSeconds(1));

            //Assert
            Assert.True(reachedMediator, "The sweep did not reach the mediator");
            var result = report.Entries[checkName];
            Assert.NotEqual(HealthStatus.Unhealthy, result.Status);
            Assert.Equal(RemedyCheckConvention.Name(remedyKind), result.Data[RemedyCheckConvention.RemedyKindKey]);
        }
        finally
        {
            var stopping = Task.Run(() => sweeper.StopAsync(CancellationToken.None));
            mediator.Open();
            await stopping.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }
}
