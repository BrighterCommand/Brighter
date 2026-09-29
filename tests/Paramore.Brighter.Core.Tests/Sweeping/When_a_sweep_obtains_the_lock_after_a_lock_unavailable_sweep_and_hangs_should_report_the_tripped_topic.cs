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

public class HungLockHolderAfterLockUnavailableSweepTests
{
    private const string InvestigateCheck = "brighter-outbox-sweeper-investigate";
    private const string TrippedTopicsGauge = "paramore.brighter.outbox_sweeper.tripped_topics";

    [Test]
    public async Task When_a_sweep_obtains_the_lock_after_a_lock_unavailable_sweep_and_hangs_should_report_the_tripped_topic()
    {
        //Arrange
        var timeProvider = new FakeTimeProvider();
        var switchableLock = new SwitchableDistributedLock();
        switchableLock.MakeUnavailable();
        var distributedLock = new CountingDistributedLock(switchableLock);
        var breaker = new InMemoryOutboxCircuitBreaker();
        breaker.TripTopic(new RoutingKey("orders"));
        using var mediator = new GatedOutboxProducerMediator();
        var services = SweeperContainer.Create(distributedLock, timeProvider, useOutboxSweeper: true,
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
            var lockUnavailable = SpinWait.SpinUntil(
                () => distributedLock.Attempts >= 1
                      && state.Read().LastOutcome == SweepOutcome.LockUnavailable
                      && !state.Read().IsSweepInProgress,
                TimeSpan.FromSeconds(10));
            switchableLock.Grant();
            timeProvider.Advance(TimeSpan.FromSeconds(10));
            var reachedMediator = mediator.WaitForSweepToStart(TimeSpan.FromSeconds(10));

            //Act
            var observations = new List<(HealthReportEntry Report, long Gauge)>();
            for (var observation = 0; observation < 3; observation++)
            {
                var report = (await healthChecks.CheckHealthAsync(registration => registration.Name == InvestigateCheck, CancellationToken.None))
                    .Entries[InvestigateCheck];
                trippedTopics.Observe();
                observations.Add((report, trippedTopics.Values.Last()));
                timeProvider.Advance(TimeSpan.FromSeconds(1));
            }

            //Assert
            await Assert.That(lockUnavailable).IsTrue().Because("The first sweep did not end as lock unavailable");
            await Assert.That(reachedMediator).IsTrue().Because("The second sweep did not reach the mediator");
            foreach (var observation in observations)
{
    await Assert.That(observation.Report.Status).IsEqualTo(HealthStatus.Degraded);
    await Assert.That(observation.Report.Status).IsNotEqualTo(HealthStatus.Unhealthy);
    await Assert.That(observation.Report.Data[RemedyCheckConvention.RemedyKindKey]).IsEqualTo(RemedyCheckConvention.Name(RemedyKind.Investigate));
    await Assert.That((await Assert.That(observation.Report.Data[RemedyCheckConvention.ConditionsKey]).IsTypeOf<string[]>())).IsEqualTo(new[] { "tripped_topics" });
    await Assert.That(observation.Report.Description).Contains("orders");
    await Assert.That(observation.Gauge).IsEqualTo(1L);
}
        }
        finally
        {
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
