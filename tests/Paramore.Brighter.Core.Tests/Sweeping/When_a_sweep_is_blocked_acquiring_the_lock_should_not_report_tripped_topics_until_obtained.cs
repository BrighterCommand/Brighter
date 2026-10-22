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
using Xunit;

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class SweepBlockedAcquiringTheLockTrippedTopicsTests
{
    private const string InvestigateCheck = "brighter-outbox-sweeper-investigate";
    private const string RestartCheck = "brighter-outbox-sweeper-restart";
    private const string TrippedTopicsGauge = "paramore.brighter.outbox_sweeper.tripped_topics";

    [Fact]
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
            Assert.True(firstCompleted, "The first sweep did not complete");
            Assert.True(reachedLock, "The second sweep did not reach the lock");
            Assert.True(reachedMediator, "The second sweep did not reach the mediator after the lock was granted");

            Assert.Equal(0L, blockedGauge);
            Assert.NotEqual(HealthStatus.Unhealthy, blockedInvestigate.Status);
            Assert.Equal(HealthStatus.Healthy, blockedInvestigate.Status);
            Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Investigate), blockedInvestigate.Data[RemedyCheckConvention.RemedyKindKey]);
            Assert.DoesNotContain("orders", blockedInvestigate.Description);

            Assert.Equal(HealthStatus.Degraded, blockedRestart.Status);
            Assert.NotEqual(HealthStatus.Unhealthy, blockedRestart.Status);
            Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Restart), blockedRestart.Data[RemedyCheckConvention.RemedyKindKey]);
            Assert.Equal(new[] { "hung_sweep" }, Assert.IsType<string[]>(blockedRestart.Data[RemedyCheckConvention.ConditionsKey]));

            Assert.Equal(1L, heldGauge);
            Assert.Equal(HealthStatus.Degraded, heldInvestigate.Status);
            Assert.NotEqual(HealthStatus.Unhealthy, heldInvestigate.Status);
            Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Investigate), heldInvestigate.Data[RemedyCheckConvention.RemedyKindKey]);
            Assert.Equal(new[] { "tripped_topics" }, Assert.IsType<string[]>(heldInvestigate.Data[RemedyCheckConvention.ConditionsKey]));
            Assert.Contains("orders", heldInvestigate.Description);
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
