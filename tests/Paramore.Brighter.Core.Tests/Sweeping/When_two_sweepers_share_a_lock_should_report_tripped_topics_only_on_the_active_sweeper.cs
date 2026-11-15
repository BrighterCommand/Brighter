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

public class SweepersSharingALockTrippedTopicsTests
{
    private const string InvestigateCheck = "brighter-outbox-sweeper-investigate";
    private const string TrippedTopicsGauge = "paramore.brighter.outbox_sweeper.tripped_topics";

    [Test]
    public async Task When_two_sweepers_share_a_lock_should_report_tripped_topics_only_on_the_active_sweeper()
    {
        //Arrange
        var timeProvider = new FakeTimeProvider();
        var sharedLock = new InMemoryLock();
        var waitingLock = new CountingDistributedLock(sharedLock);
        using var activeMediator = new GatedOutboxProducerMediator();
        using var waiterMediator = new GatedOutboxProducerMediator().CompleteFirst(1);
        using var active = new SweeperHost(sharedLock, activeMediator, timeProvider);
        using var waiter = new SweeperHost(waitingLock, waiterMediator, timeProvider);
        active.Breaker.TripTopic(new RoutingKey("orders"));
        waiter.Breaker.TripTopic(new RoutingKey("orders"));

        try
        {
            //Act
            await active.Sweeper.StartAsync(CancellationToken.None);
            var activeReachedMediator = activeMediator.WaitForSweepToStart(TimeSpan.FromSeconds(10));
            await waiter.Sweeper.StartAsync(CancellationToken.None);
            var waiterLockUnavailable = SpinWait.SpinUntil(
                () => waitingLock.Attempts >= 1 && !waiter.State.Read().IsSweepInProgress, TimeSpan.FromSeconds(10));

            var activeReport = (await active.HealthChecks.CheckHealthAsync(CancellationToken.None)).Entries[InvestigateCheck];
            active.TrippedTopics.Observe();
            var activeGauge = active.TrippedTopics.Values.Last();
            var waiterReport = (await waiter.HealthChecks.CheckHealthAsync(CancellationToken.None)).Entries[InvestigateCheck];
            waiter.TrippedTopics.Observe();
            var waiterGauge = waiter.TrippedTopics.Values.Last();

            var stopping = active.Sweeper.StopAsync(CancellationToken.None);
            activeMediator.Open();
            await stopping.WaitAsync(TimeSpan.FromSeconds(10));
            timeProvider.Advance(TimeSpan.FromSeconds(10));
            var waiterCompleted = SpinWait.SpinUntil(
                () => waitingLock.Attempts >= 2 && !waiter.State.Read().IsSweepInProgress, TimeSpan.FromSeconds(10));
            var takenOverReport = (await waiter.HealthChecks.CheckHealthAsync(CancellationToken.None)).Entries[InvestigateCheck];
            waiter.TrippedTopics.Observe();
            var takenOverGauge = waiter.TrippedTopics.Values.Last();

            //Assert
            await Assert.That(activeReport.Status).IsEqualTo(HealthStatus.Degraded);
            await Assert.That(activeReport.Status).IsNotEqualTo(HealthStatus.Unhealthy);
            await Assert.That(activeReport.Data[RemedyCheckConvention.RemedyKindKey]).IsEqualTo(RemedyCheckConvention.Name(RemedyKind.Investigate));
            await Assert.That((await Assert.That(activeReport.Data[RemedyCheckConvention.ConditionsKey]).IsTypeOf<string[]>())).IsEqualTo(new[] { "tripped_topics" });
            await Assert.That(activeReport.Description).Contains("orders");
            await Assert.That(activeGauge).IsEqualTo(1L);

            await Assert.That(waiterReport.Status).IsEqualTo(HealthStatus.Healthy);
            await Assert.That(waiterReport.Status).IsNotEqualTo(HealthStatus.Unhealthy);
            await Assert.That(waiterReport.Data[RemedyCheckConvention.RemedyKindKey]).IsEqualTo(RemedyCheckConvention.Name(RemedyKind.Investigate));
            await Assert.That((await Assert.That(waiterReport.Data[RemedyCheckConvention.ConditionsKey]).IsTypeOf<string[]>())).IsEmpty();
            await Assert.That(waiterGauge).IsEqualTo(0L);

            await Assert.That(takenOverReport.Status).IsEqualTo(HealthStatus.Degraded);
            await Assert.That(takenOverReport.Status).IsNotEqualTo(HealthStatus.Unhealthy);
            await Assert.That(takenOverReport.Data[RemedyCheckConvention.RemedyKindKey]).IsEqualTo(RemedyCheckConvention.Name(RemedyKind.Investigate));
            await Assert.That((await Assert.That(takenOverReport.Data[RemedyCheckConvention.ConditionsKey]).IsTypeOf<string[]>())).IsEqualTo(new[] { "tripped_topics" });
            await Assert.That(takenOverReport.Description).Contains("orders");
            await Assert.That(takenOverGauge).IsEqualTo(1L);

            await Assert.That(activeReachedMediator).IsTrue().Because("The first sweeper's sweep did not reach the mediator");
            await Assert.That(waiterLockUnavailable).IsTrue().Because("The second sweeper's lock-unavailable sweep did not end");
            await Assert.That(waiterCompleted).IsTrue().Because("The second sweeper did not complete a sweep after the first released the lock");
        }
        finally
        {
            await waiter.Sweeper.StopAsync(new CancellationToken(canceled: true));
            await active.Sweeper.StopAsync(new CancellationToken(canceled: true));
            activeMediator.Open();
        }
    }

    private sealed class SweeperHost : IDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly MeterProvider _meterProvider;

        public SweeperHost(IDistributedLock distributedLock, IAmAnOutboxProducerMediator mediator, TimeProvider timeProvider)
        {
            Breaker = new InMemoryOutboxCircuitBreaker();
            var services = SweeperContainer.Create(distributedLock, timeProvider, useOutboxSweeper: true,
                configureSweeper: options => options.TimerInterval = 10);
            services.AddSingleton(mediator);
            services.AddSingleton<IAmAnOutboxCircuitBreaker>(Breaker);
            services.AddLogging();
            services.AddMetrics();
            services.AddHealthChecks().AddBrighterOutboxSweeperHealthChecks();
            services.AddOpenTelemetry().WithMetrics(metrics => metrics.AddBrighterInstrumentation());
            _provider = services.BuildServiceProvider();
            _meterProvider = _provider.GetRequiredService<MeterProvider>();
            TrippedTopics = new TrippedTopicsListener(_provider.GetRequiredService<IMeterFactory>());
            State = _provider.GetRequiredService<OutboxSweeperState>();
            HealthChecks = _provider.GetRequiredService<HealthCheckService>();
            Sweeper = _provider.GetServices<IHostedService>().OfType<TimedOutboxSweeper>().Single();
        }

        public InMemoryOutboxCircuitBreaker Breaker { get; }
        public TimedOutboxSweeper Sweeper { get; }
        public OutboxSweeperState State { get; }
        public HealthCheckService HealthChecks { get; }
        public TrippedTopicsListener TrippedTopics { get; }

        public void Dispose()
        {
            TrippedTopics.Dispose();
            _meterProvider.Dispose();
            _provider.Dispose();
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
