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
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class ActiveHungSweepWithTrippedTopicTests
{
    private const string RestartCheck = "brighter-outbox-sweeper-restart";
    private const string InvestigateCheck = "brighter-outbox-sweeper-investigate";

    [Fact]
    public async Task When_an_active_sweep_hangs_with_a_topic_tripped_should_degrade_both_checks()
    {
        //Arrange
        var timeProvider = new FakeTimeProvider();
        var breaker = new InMemoryOutboxCircuitBreaker();
        breaker.TripTopic(new RoutingKey("orders"));
        using var mediator = new GatedOutboxProducerMediator().CompleteFirst(1);
        var services = SweeperContainer.Create(new InMemoryLock(), timeProvider, useOutboxSweeper: true,
            configureSweeper: options => options.TimerInterval = 10);
        services.AddSingleton<IAmAnOutboxProducerMediator>(mediator);
        services.AddSingleton<IAmAnOutboxCircuitBreaker>(breaker);
        services.AddLogging();
        services.AddHealthChecks().AddBrighterOutboxSweeperHealthChecks();
        using var provider = services.BuildServiceProvider();
        var state = provider.GetRequiredService<OutboxSweeperState>();
        var healthChecks = provider.GetRequiredService<HealthCheckService>();
        var sweeper = provider.GetServices<IHostedService>().OfType<TimedOutboxSweeper>().Single();

        try
        {
            await sweeper.StartAsync(CancellationToken.None);
            var firstCompleted = SpinWait.SpinUntil(
                () => state.Read().LastOutcome == SweepOutcome.Completed && !state.Read().IsSweepInProgress, TimeSpan.FromSeconds(10));
            timeProvider.Advance(TimeSpan.FromSeconds(10));
            var reachedMediator = mediator.WaitForSweepToStart(TimeSpan.FromSeconds(10));
            timeProvider.Advance(TimeSpan.FromSeconds(31));

            //Act
            var report = await healthChecks.CheckHealthAsync(CancellationToken.None);

            //Assert
            Assert.True(firstCompleted, "The previous sweep did not complete");
            Assert.True(reachedMediator, "The current sweep did not reach the mediator");

            var restart = report.Entries[RestartCheck];
            Assert.Equal(HealthStatus.Degraded, restart.Status);
            Assert.NotEqual(HealthStatus.Unhealthy, restart.Status);
            Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Restart), restart.Data[RemedyCheckConvention.RemedyKindKey]);
            Assert.Equal(new[] { "hung_sweep" }, Assert.IsType<string[]>(restart.Data[RemedyCheckConvention.ConditionsKey]));
            Assert.Contains("31", restart.Description);
            Assert.Contains("30", restart.Description);

            var investigate = report.Entries[InvestigateCheck];
            Assert.Equal(HealthStatus.Degraded, investigate.Status);
            Assert.NotEqual(HealthStatus.Unhealthy, investigate.Status);
            Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Investigate), investigate.Data[RemedyCheckConvention.RemedyKindKey]);
            Assert.Equal(new[] { "tripped_topics" }, Assert.IsType<string[]>(investigate.Data[RemedyCheckConvention.ConditionsKey]));
            Assert.Contains("orders", investigate.Description);
        }
        finally
        {
            mediator.Open();
            await sweeper.StopAsync(new CancellationToken(canceled: true));
        }
    }
}
