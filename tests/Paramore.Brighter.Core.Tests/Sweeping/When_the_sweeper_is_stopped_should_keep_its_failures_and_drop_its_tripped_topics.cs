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
using Paramore.Brighter.Core.Tests.Observability.Metrics;
using Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;
using Paramore.Brighter.Extensions.Diagnostics;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class StoppedSweeperFailuresAndTrippedTopicsTests
{
    private const string RestartCheck = "brighter-outbox-sweeper-restart";
    private const string InvestigateCheck = "brighter-outbox-sweeper-investigate";
    private const string TrippedTopicsGauge = "paramore.brighter.outbox_sweeper.tripped_topics";

    [Test]
    public async Task When_the_sweeper_is_stopped_should_keep_its_failures_and_drop_its_tripped_topics()
    {
        //Arrange
        var failingMediator = new SwitchableOutboxProducerMediator();
        failingMediator.Throw("every sweep fails");
        using var afterFailures = new StoppedSweeperRig(failingMediator, trippedTopic: null);
        var completingMediator = new SwitchableOutboxProducerMediator();
        completingMediator.Complete();
        using var afterCompleted = new StoppedSweeperRig(completingMediator, trippedTopic: "orders");
        using var blockingMediator = new GatedOutboxProducerMediator();
        using var afterAbandoned = new StoppedSweeperRig(blockingMediator, trippedTopic: "orders");

        try
        {
            //Act
            await afterFailures.Sweeper.StartAsync(CancellationToken.None);
            var firstFailed = afterFailures.WaitForSweep(() => failingMediator.SweepsAttempted, 1, SweepOutcome.Failed);
            afterFailures.Advance(10);
            var secondFailed = afterFailures.WaitForSweep(() => failingMediator.SweepsAttempted, 2, SweepOutcome.Failed);
            afterFailures.Advance(10);
            var thirdFailed = afterFailures.WaitForSweep(() => failingMediator.SweepsAttempted, 3, SweepOutcome.Failed);
            await afterFailures.Sweeper.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));
            var failuresEntry = (await afterFailures.CheckInvestigate()).Entries[InvestigateCheck];

            await afterCompleted.Sweeper.StartAsync(CancellationToken.None);
            var completed = afterCompleted.WaitForSweep(() => completingMediator.SweepsAttempted, 1, SweepOutcome.Completed);
            await afterCompleted.Sweeper.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));
            var completedEntry = (await afterCompleted.CheckInvestigate()).Entries[InvestigateCheck];
            afterCompleted.ObserveGauges();

            await afterAbandoned.Sweeper.StartAsync(CancellationToken.None);
            var reachedMediator = blockingMediator.WaitForSweepToStart(TimeSpan.FromSeconds(10));
            afterAbandoned.Advance(25);
            await afterAbandoned.Sweeper.StopAsync(new CancellationToken(canceled: true));
            afterAbandoned.Advance(31);
            afterAbandoned.ObserveGauges();
            var abandonedReport = await afterAbandoned.HealthChecks.CheckHealthAsync(CancellationToken.None);

            //Assert
            await Assert.That(firstFailed && secondFailed && thirdFailed).IsTrue().Because("The failing sweeps did not all end");
            await Assert.That(failuresEntry.Status).IsEqualTo(HealthStatus.Degraded);
            await Assert.That(failuresEntry.Status).IsNotEqualTo(HealthStatus.Unhealthy);
            await Assert.That(failuresEntry.Data[RemedyCheckConvention.RemedyKindKey]).IsEqualTo(RemedyCheckConvention.Name(RemedyKind.Investigate));
            await Assert.That((await Assert.That(failuresEntry.Data[RemedyCheckConvention.ConditionsKey]).IsTypeOf<string[]>())).IsEqualTo(new[] { "consecutive_failed_sweeps" });

            await Assert.That(completed).IsTrue().Because("The sweep did not complete");
            await Assert.That(completedEntry.Status).IsEqualTo(HealthStatus.Healthy);
            await Assert.That(completedEntry.Status).IsNotEqualTo(HealthStatus.Unhealthy);
            await Assert.That(completedEntry.Data[RemedyCheckConvention.RemedyKindKey]).IsEqualTo(RemedyCheckConvention.Name(RemedyKind.Investigate));
            await Assert.That((await Assert.That(completedEntry.Data[RemedyCheckConvention.ConditionsKey]).IsTypeOf<string[]>())).IsEmpty();
            await Assert.That(afterCompleted.TrippedTopics.Values).IsEqualTo([0L]);
            await Assert.That(afterCompleted.RunningTime.Values).IsEqualTo([0.0]);

            await Assert.That(reachedMediator).IsTrue().Because("The sweep did not reach the mediator");
            await Assert.That(afterAbandoned.TrippedTopics.Values).IsEqualTo([0L]);
            await Assert.That(afterAbandoned.RunningTime.Values).IsEqualTo([56.0]);
            var restart = abandonedReport.Entries[RestartCheck];
            await Assert.That(restart.Status).IsEqualTo(HealthStatus.Degraded);
            await Assert.That(restart.Status).IsNotEqualTo(HealthStatus.Unhealthy);
            await Assert.That(restart.Data[RemedyCheckConvention.RemedyKindKey]).IsEqualTo(RemedyCheckConvention.Name(RemedyKind.Restart));
            await Assert.That((await Assert.That(restart.Data[RemedyCheckConvention.ConditionsKey]).IsTypeOf<string[]>())).IsEqualTo(new[] { "hung_sweep" });
            await Assert.That(restart.Description).Contains("56");
        }
        finally
        {
            blockingMediator.Open();
            afterFailures.Sweeper.Dispose();
            afterCompleted.Sweeper.Dispose();
            afterAbandoned.Sweeper.Dispose();
        }
    }

    private sealed class StoppedSweeperRig : IDisposable
    {
        private readonly FakeTimeProvider _timeProvider = new();
        private readonly ServiceProvider _provider;
        private readonly MeterProvider _meterProvider;
        private readonly OutboxSweeperState _state;

        public StoppedSweeperRig(IAmAnOutboxProducerMediator mediator, string? trippedTopic)
        {
            var breaker = new InMemoryOutboxCircuitBreaker();
            if (trippedTopic is not null) breaker.TripTopic(new RoutingKey(trippedTopic));
            var services = SweeperContainer.Create(new InMemoryLock(), _timeProvider, useOutboxSweeper: true,
                configureSweeper: options => options.TimerInterval = 10);
            services.AddSingleton(mediator);
            services.AddSingleton<IAmAnOutboxCircuitBreaker>(breaker);
            services.AddLogging();
            services.AddMetrics();
            services.AddHealthChecks().AddBrighterOutboxSweeperHealthChecks();
            services.AddOpenTelemetry().WithMetrics(metrics => metrics.AddBrighterInstrumentation());
            _provider = services.BuildServiceProvider();
            _meterProvider = _provider.GetRequiredService<MeterProvider>();
            var meterFactory = _provider.GetRequiredService<IMeterFactory>();
            RunningTime = new RunningTimeListener(meterFactory);
            TrippedTopics = new TrippedTopicsListener(meterFactory);
            _state = _provider.GetRequiredService<OutboxSweeperState>();
            HealthChecks = _provider.GetRequiredService<HealthCheckService>();
            Sweeper = _provider.GetServices<IHostedService>().OfType<TimedOutboxSweeper>().Single();
        }

        public TimedOutboxSweeper Sweeper { get; }
        public HealthCheckService HealthChecks { get; }
        public RunningTimeListener RunningTime { get; }
        public TrippedTopicsListener TrippedTopics { get; }

        public void Advance(int seconds) => _timeProvider.Advance(TimeSpan.FromSeconds(seconds));

        public Task<HealthReport> CheckInvestigate() =>
            HealthChecks.CheckHealthAsync(registration => registration.Name == InvestigateCheck, CancellationToken.None);

        public void ObserveGauges()
        {
            TrippedTopics.Observe();
            RunningTime.Observe();
        }

        public bool WaitForSweep(Func<int> attempted, int count, SweepOutcome outcome) =>
            SpinWait.SpinUntil(
                () => attempted() >= count && _state.Read().LastOutcome == outcome && !_state.Read().IsSweepInProgress,
                TimeSpan.FromSeconds(10));

        public void Dispose()
        {
            RunningTime.Dispose();
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
