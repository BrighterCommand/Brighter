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
using Microsoft.Extensions.Time.Testing;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;
using Paramore.Brighter.Extensions.Diagnostics;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class SweepBlockedAcquiringTheLockTrippedTopicsTests
{
    private const string InvestigateCheck = "brighter-outbox-sweeper-investigate";
    private const string RestartCheck = "brighter-outbox-sweeper-restart";
    private const string TrippedTopicsGauge = "paramore.brighter.outbox_sweeper.tripped_topics";

    [Test]
    public async Task When_a_sweep_is_blocked_acquiring_the_lock_should_not_report_tripped_topics_until_obtained()
    {
        //Arrange
        var timeProvider = new FakeTimeProvider();
        using var lockGate = new GatedDistributedLock().PassFirst(1);
        var breaker = new InMemoryOutboxCircuitBreaker();
        breaker.TripTopic(new RoutingKey("orders"));
        using var mediator = new GatedOutboxProducerMediator().CompleteFirst(1);
        var services = SweeperContainer.Create(lockGate, timeProvider, useOutboxSweeper: true,
            configureSweeper: options => options.TimerInterval = 10);
        services.AddSingleton<IAmAnOutboxProducerMediator>(mediator);
        services.AddSingleton<IAmAnOutboxCircuitBreaker>(breaker);
        services.AddLogging();
        services.AddMetrics();
        services.AddHealthChecks().AddBrighterOutboxSweeperHealthChecks();
        services.AddOpenTelemetry().WithMetrics(metrics => metrics.AddBrighterInstrumentation());
        using var provider = services.BuildServiceProvider();
        using var meterProvider = provider.GetRequiredService<MeterProvider>();
        using var trippedTopics = new TrippedTopicsListener(provider.GetRequiredService<IMeterFactory>());
        var state = provider.GetRequiredService<OutboxSweeperState>();
        var healthChecks = provider.GetRequiredService<HealthCheckService>();
        var sweeper = provider.GetServices<IHostedService>().OfType<TimedOutboxSweeper>().Single();

        try
        {
            await sweeper.StartAsync(CancellationToken.None);
            var firstCompleted = SpinWait.SpinUntil(
                () => state.Read().LastOutcome == SweepOutcome.Completed && !state.Read().IsSweepInProgress,
                TimeSpan.FromSeconds(10));
            timeProvider.Advance(TimeSpan.FromSeconds(10));
            var reachedLock = lockGate.WaitForSweepToStart(TimeSpan.FromSeconds(10));
            timeProvider.Advance(TimeSpan.FromSeconds(31));

            //Act
            var blockedInvestigate = (await healthChecks.CheckHealthAsync(registration => registration.Name == InvestigateCheck, CancellationToken.None))
                .Entries[InvestigateCheck];
            var blockedRestart = (await healthChecks.CheckHealthAsync(registration => registration.Name == RestartCheck, CancellationToken.None))
                .Entries[RestartCheck];
            trippedTopics.Observe();
            var blockedGauge = trippedTopics.Values.Last();

            lockGate.Open();
            var reachedMediator = mediator.WaitForSweepToStart(TimeSpan.FromSeconds(10));
            var heldInvestigate = (await healthChecks.CheckHealthAsync(registration => registration.Name == InvestigateCheck, CancellationToken.None))
                .Entries[InvestigateCheck];
            trippedTopics.Observe();
            var heldGauge = trippedTopics.Values.Last();

            //Assert
            await Assert.That(firstCompleted).IsTrue().Because("The first sweep did not complete");
            await Assert.That(reachedLock).IsTrue().Because("The second sweep did not reach the lock");
            await Assert.That(reachedMediator).IsTrue().Because("The second sweep did not reach the mediator after the lock was granted");

            await Assert.That(blockedGauge).IsEqualTo(0L);
            await Assert.That(blockedInvestigate.Status).IsNotEqualTo(HealthStatus.Unhealthy);
            await Assert.That(blockedInvestigate.Status).IsEqualTo(HealthStatus.Healthy);
            await Assert.That(blockedInvestigate.Data[RemedyCheckConvention.RemedyKindKey]).IsEqualTo(RemedyCheckConvention.Name(RemedyKind.Investigate));
            await Assert.That(blockedInvestigate.Description).DoesNotContain("orders");

            await Assert.That(blockedRestart.Status).IsEqualTo(HealthStatus.Degraded);
            await Assert.That(blockedRestart.Status).IsNotEqualTo(HealthStatus.Unhealthy);
            await Assert.That(blockedRestart.Data[RemedyCheckConvention.RemedyKindKey]).IsEqualTo(RemedyCheckConvention.Name(RemedyKind.Restart));
            await Assert.That((await Assert.That(blockedRestart.Data[RemedyCheckConvention.ConditionsKey]).IsTypeOf<string[]>())).IsEqualTo(new[] { "hung_sweep" });

            await Assert.That(heldGauge).IsEqualTo(1L);
            await Assert.That(heldInvestigate.Status).IsEqualTo(HealthStatus.Degraded);
            await Assert.That(heldInvestigate.Status).IsNotEqualTo(HealthStatus.Unhealthy);
            await Assert.That(heldInvestigate.Data[RemedyCheckConvention.RemedyKindKey]).IsEqualTo(RemedyCheckConvention.Name(RemedyKind.Investigate));
            await Assert.That((await Assert.That(heldInvestigate.Data[RemedyCheckConvention.ConditionsKey]).IsTypeOf<string[]>())).IsEqualTo(new[] { "tripped_topics" });
            await Assert.That(heldInvestigate.Description).Contains("orders");
        }
        finally
        {
            lockGate.Open();
            mediator.Open();
            await sweeper.StopAsync(new CancellationToken(canceled: true));
        }
    }

    private sealed class TrippedTopicsListener : IDisposable
    {
        private readonly MeterListener _listener = new();
        private readonly List<long> _values = [];

        public TrippedTopicsListener(IMeterFactory meterFactory)
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (!ReferenceEquals(instrument.Meter.Scope, meterFactory) || instrument.Name != TrippedTopicsGauge) return;
                listener.EnableMeasurementEvents(instrument);
            };
            _listener.SetMeasurementEventCallback<long>((_, value, _, _) => _values.Add(value));
            _listener.Start();
        }

        public IReadOnlyList<long> Values => _values;

        public void Observe() => _listener.RecordObservableInstruments();

        public void Dispose() => _listener.Dispose();
    }
}
