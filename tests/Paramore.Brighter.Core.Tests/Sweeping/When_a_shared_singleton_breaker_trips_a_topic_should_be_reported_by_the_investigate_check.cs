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

#nullable enable

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

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class InvestigateCheckReadsBreakerRegisteredInContainerTests
{
    private const string InvestigateCheck = "brighter-outbox-sweeper-investigate";

    [Test]
    public async Task When_a_shared_singleton_breaker_trips_a_topic_should_be_reported_by_the_investigate_check()
    {
        //Arrange
        var breaker = new InMemoryOutboxCircuitBreaker();
        using var mediator = new GatedOutboxProducerMediator();
        var services = SweeperContainer.Create(new InMemoryLock(), new FakeTimeProvider(), useOutboxSweeper: true);
        services.AddSingleton<IAmAnOutboxCircuitBreaker>(breaker);
        services.AddSingleton<IAmAnOutboxProducerMediator>(mediator);
        services.AddLogging();
        services.AddMetrics();
        services.AddHealthChecks().AddBrighterOutboxSweeperHealthChecks();
        using var provider = services.BuildServiceProvider();
        var sweeper = provider.GetServices<IHostedService>().OfType<TimedOutboxSweeper>().Single();

        try
        {
            await sweeper.StartAsync(CancellationToken.None);
            var reachedMediator = mediator.WaitForSweepToStart(TimeSpan.FromSeconds(10));

            //Act
            breaker.TripTopic(new RoutingKey("payments"));
            var activeBeforeCheck = provider.GetRequiredService<OutboxSweeperState>().Read().IsActive;
            var report = await provider.GetRequiredService<HealthCheckService>().CheckHealthAsync(CancellationToken.None);

            //Assert
            await Assert.That(reachedMediator).IsTrue().Because("The sweep did not reach the mediator");
            await Assert.That(activeBeforeCheck).IsTrue();
            var investigate = report.Entries[InvestigateCheck];
            await Assert.That(investigate.Status).IsEqualTo(HealthStatus.Degraded);
            await Assert.That(investigate.Status).IsNotEqualTo(HealthStatus.Unhealthy);
            await Assert.That(investigate.Description).Contains("payments");
            await Assert.That(investigate.Data[RemedyCheckConvention.RemedyKindKey]).IsEqualTo(RemedyCheckConvention.Name(RemedyKind.Investigate));
            await Assert.That((await Assert.That(investigate.Data[RemedyCheckConvention.ConditionsKey]).IsTypeOf<string[]>())).IsEqualTo(new[] { "tripped_topics" });
        }
        finally
        {
            mediator.Open();
            await sweeper.StopAsync(new CancellationToken(canceled: true));
        }
    }

    [Test]
    public async Task When_no_breaker_is_registered_should_report_no_tripped_topic_condition_and_not_throw()
    {
        //Arrange
        using var mediator = new GatedOutboxProducerMediator();
        var services = SweeperContainer.Create(new InMemoryLock(), new FakeTimeProvider(), useOutboxSweeper: true);
        services.AddSingleton<IAmAnOutboxProducerMediator>(mediator);
        services.AddLogging();
        services.AddMetrics();
        services.AddHealthChecks().AddBrighterOutboxSweeperHealthChecks();
        using var provider = services.BuildServiceProvider();
        var sweeper = provider.GetServices<IHostedService>().OfType<TimedOutboxSweeper>().Single();

        try
        {
            await sweeper.StartAsync(CancellationToken.None);
            var reachedMediator = mediator.WaitForSweepToStart(TimeSpan.FromSeconds(10));

            //Act
            var activeBeforeCheck = provider.GetRequiredService<OutboxSweeperState>().Read().IsActive;
            HealthReport? report = null;
            var exception = await TestExceptionRecorder.CaptureAsync(async () =>
                report = await provider.GetRequiredService<HealthCheckService>().CheckHealthAsync(CancellationToken.None));

            //Assert
            await Assert.That(exception).IsNull();
            await Assert.That(reachedMediator).IsTrue().Because("The sweep did not reach the mediator");
            await Assert.That(activeBeforeCheck).IsTrue();
            var investigate = report!.Entries[InvestigateCheck];
            await Assert.That(investigate.Status).IsEqualTo(HealthStatus.Healthy);
            await Assert.That(investigate.Status).IsNotEqualTo(HealthStatus.Unhealthy);
            await Assert.That(investigate.Data[RemedyCheckConvention.RemedyKindKey]).IsEqualTo(RemedyCheckConvention.Name(RemedyKind.Investigate));
            await Assert.That((await Assert.That(investigate.Data[RemedyCheckConvention.ConditionsKey]).IsTypeOf<string[]>())).IsEmpty();
        }
        finally
        {
            mediator.Open();
            await sweeper.StopAsync(new CancellationToken(canceled: true));
        }
    }
}
