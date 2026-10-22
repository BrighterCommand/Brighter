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

public class TrippedTopicSignalsLockCollisionGapTests
{
    private const string InvestigateCheck = "brighter-outbox-sweeper-investigate";
    private const string TrippedTopicsGauge = "paramore.brighter.outbox_sweeper.tripped_topics";

    [Fact]
    public async Task When_an_active_sweepers_next_sweep_collides_on_the_lock_should_drop_and_restore_tripped_topics()
    {
        //Arrange
        var timeProvider = new FakeTimeProvider();
        var mediator = new SwitchableOutboxProducerMediator();
        var switchableLock = new SwitchableDistributedLock();
        var breaker = new InMemoryOutboxCircuitBreaker();
        breaker.TripTopic(new RoutingKey("orders"));
        var services = SweeperContainer.Create(switchableLock, timeProvider, useOutboxSweeper: true,
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
            //Act
            await sweeper.StartAsync(CancellationToken.None);
            var firstCompleted = WaitForSweep(state, mediator, 1, SweepOutcome.Completed);
            var beforeCollision = await Observe(healthChecks, trippedTopics, timeProvider);

            switchableLock.MakeUnavailable();
            timeProvider.Advance(TimeSpan.FromSeconds(5));
            var collided = WaitForSweep(state, mediator, 1, SweepOutcome.LockUnavailable);
            var duringCollision = await Observe(healthChecks, trippedTopics, timeProvider);

            switchableLock.Grant();
            timeProvider.Advance(TimeSpan.FromSeconds(5));
            var thirdCompleted = WaitForSweep(state, mediator, 2, SweepOutcome.Completed);
            var afterCollision = await Observe(healthChecks, trippedTopics, timeProvider);

            //Assert
            Assert.True(firstCompleted && collided && thirdCompleted, "The sweeps did not all end");
            AssertTripped(beforeCollision);
            AssertTripped(afterCollision);
            Assert.Equal(HealthStatus.Healthy, duringCollision.Report.Status);
            Assert.NotEqual(HealthStatus.Unhealthy, duringCollision.Report.Status);
            Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Investigate), duringCollision.Report.Data[RemedyCheckConvention.RemedyKindKey]);
            Assert.DoesNotContain("tripped_topics", Assert.IsType<string[]>(duringCollision.Report.Data[RemedyCheckConvention.ConditionsKey]));
            Assert.Equal(0L, duringCollision.Gauge);
        }
        finally
        {
            await sweeper.StopAsync(new CancellationToken(canceled: true));
        }
    }

    private static void AssertTripped((HealthReportEntry Report, long Gauge) observation)
    {
        Assert.Equal(HealthStatus.Degraded, observation.Report.Status);
        Assert.NotEqual(HealthStatus.Unhealthy, observation.Report.Status);
        Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Investigate), observation.Report.Data[RemedyCheckConvention.RemedyKindKey]);
        Assert.Equal(new[] { "tripped_topics" }, Assert.IsType<string[]>(observation.Report.Data[RemedyCheckConvention.ConditionsKey]));
        Assert.Contains("orders", observation.Report.Description);
        Assert.Equal(1L, observation.Gauge);
    }

    private static async Task<(HealthReportEntry Report, long Gauge)> Observe(
        HealthCheckService healthChecks, TrippedTopicsListener trippedTopics, FakeTimeProvider timeProvider)
    {
        timeProvider.Advance(TimeSpan.FromSeconds(5));
        var report = (await healthChecks.CheckHealthAsync(registration => registration.Name == InvestigateCheck, CancellationToken.None))
            .Entries[InvestigateCheck];
        trippedTopics.Observe();
        return (report, trippedTopics.Values.Last());
    }

    private static bool WaitForSweep(OutboxSweeperState state, SwitchableOutboxProducerMediator mediator, int mediatorSweeps, SweepOutcome outcome) =>
        SpinWait.SpinUntil(
            () => mediator.SweepsAttempted >= mediatorSweeps && state.Read().LastOutcome == outcome && !state.Read().IsSweepInProgress,
            TimeSpan.FromSeconds(10));

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
