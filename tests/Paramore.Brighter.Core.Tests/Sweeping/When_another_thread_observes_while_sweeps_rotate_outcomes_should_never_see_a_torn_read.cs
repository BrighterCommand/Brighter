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
using Paramore.Brighter.Core.Tests.Observability.Metrics;
using Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;
using Paramore.Brighter.Extensions.Diagnostics;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class TornReadTests
{
    private const string TrippedTopicsGauge = "paramore.brighter.outbox_sweeper.tripped_topics";
    private const int Rounds = 40;
    private const int ObservationsInPause = 20;
    private const int ObservationsDuringSweep = 5;
    private static readonly TimeSpan Guard = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task When_another_thread_observes_while_sweeps_rotate_outcomes_should_never_see_a_torn_read()
    {
        //Arrange
        var timeProvider = new FakeTimeProvider();
        var countingTimeProvider = new TimerCountingTimeProvider(timeProvider);
        var halfInterval = TimeSpan.FromSeconds(5);
        var mediator = new SwitchableOutboxProducerMediator();
        mediator.Complete();
        var sweepLock = new SwitchableDistributedLock();
        var breaker = new InMemoryOutboxCircuitBreaker();
        breaker.TripTopic(new RoutingKey("orders"));
        var services = SweeperContainer.Create(sweepLock, countingTimeProvider, useOutboxSweeper: true,
            configureSweeper: options => options.TimerInterval = 10);
        services.AddSingleton<IAmAnOutboxProducerMediator>(mediator);
        services.AddSingleton<IAmAnOutboxCircuitBreaker>(breaker);
        services.AddLogging();
        services.AddMetrics();
        services.AddHealthChecks().AddBrighterOutboxSweeperHealthChecks();
        services.AddOpenTelemetry().WithMetrics(metrics => metrics.AddBrighterInstrumentation());
        using var provider = services.BuildServiceProvider();
        using var meterProvider = provider.GetRequiredService<MeterProvider>();
        var meterFactory = provider.GetRequiredService<IMeterFactory>();
        using var runningTime = new RunningTimeListener(meterFactory);
        using var trippedTopics = new TrippedTopicsListener(meterFactory);
        var state = provider.GetRequiredService<OutboxSweeperState>();
        var healthChecks = provider.GetRequiredService<HealthCheckService>();
        var sweeper = provider.GetServices<IHostedService>().OfType<TimedOutboxSweeper>().Single();
        var observer = new Observer(state, healthChecks, runningTime, trippedTopics, Rounds * (ObservationsInPause + ObservationsDuringSweep));
        var observerThread = new Thread(observer.Run) { IsBackground = true };
        var rotation = new[] { SweepOutcome.Failed, SweepOutcome.LockUnavailable, SweepOutcome.Completed };

        try
        {
            //Act
            await sweeper.StartAsync(CancellationToken.None);
            var firstEnded = WaitForOutcome(state, SweepOutcome.Completed);
            observerThread.Start();
            var allSweepsEnded = firstEnded;
            var allObserved = true;
            var allParked = true;
            for (var round = 0; round < Rounds && allSweepsEnded && allObserved && allParked; round++)
            {
                var outcome = rotation[round % rotation.Length];
                allParked &= countingTimeProvider.WaitForTimers(round + 1, Guard);
                timeProvider.Advance(halfInterval);
                allObserved &= observer.Allow(ObservationsInPause);

                Prepare(outcome, mediator, sweepLock);
                timeProvider.Advance(halfInterval);
                observer.AllowWithoutWaiting(ObservationsDuringSweep);
                allSweepsEnded &= WaitForOutcome(state, outcome);
                allObserved &= observer.WaitForAllAllowed();
            }

            observerThread.Join(Guard);
            await sweeper.StopAsync(CancellationToken.None).WaitAsync(Guard);

            //Assert
            Assert.True(firstEnded, "The first sweep did not end");
            Assert.True(allParked, "The sweeper did not park between sweeps");
            Assert.True(allSweepsEnded, "A rotating sweep did not end");
            Assert.True(allObserved, "The observer did not take every observation it was allowed");
            Assert.Equal(Rounds * (ObservationsInPause + ObservationsDuringSweep), observer.Observations);
            Assert.Empty(observer.Violations);
        }
        finally
        {
            await sweeper.StopAsync(new CancellationToken(canceled: true));
            observer.Cancel();
        }
    }

    private static void Prepare(SweepOutcome outcome, SwitchableOutboxProducerMediator mediator, SwitchableDistributedLock sweepLock)
    {
        if (outcome == SweepOutcome.LockUnavailable) sweepLock.MakeUnavailable();
        else sweepLock.Grant();

        if (outcome == SweepOutcome.Failed) mediator.Throw("the sweep failed");
        else mediator.Complete();
    }

    private static bool WaitForOutcome(OutboxSweeperState state, SweepOutcome outcome) =>
        SpinWait.SpinUntil(() =>
        {
            var snapshot = state.Read();
            return !snapshot.IsSweepInProgress && snapshot.LastOutcome == outcome;
        }, Guard);

    private sealed class Observer(
        OutboxSweeperState state,
        HealthCheckService healthChecks,
        RunningTimeListener runningTime,
        TrippedTopicsListener trippedTopics,
        int total)
    {
        private readonly SemaphoreSlim _permits = new(0);
        private readonly CancellationTokenSource _cancellation = new();
        private readonly List<string> _violations = [];
        private int _observations;
        private int _allowed;

        public int Observations => Volatile.Read(ref _observations);

        public IReadOnlyList<string> Violations => _violations;

        public bool Allow(int count)
        {
            AllowWithoutWaiting(count);
            return WaitForAllAllowed();
        }

        public void AllowWithoutWaiting(int count)
        {
            Interlocked.Add(ref _allowed, count);
            _permits.Release(count);
        }

        public bool WaitForAllAllowed() =>
            SpinWait.SpinUntil(() => Observations >= Volatile.Read(ref _allowed), Guard);

        public void Cancel() => _cancellation.Cancel();

        public void Run()
        {
            var previousCount = 0;
            try
            {
                for (var i = 0; i < total; i++)
                {
                    _permits.Wait(_cancellation.Token);
                    previousCount = Observe(previousCount);
                    Interlocked.Increment(ref _observations);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private int Observe(int previousCount)
        {
            try
            {
                runningTime.Observe();
                trippedTopics.Observe();
                var snapshot = state.Read();
                var report = healthChecks.CheckHealthAsync(CancellationToken.None).GetAwaiter().GetResult();

                CheckGauges();
                CheckReport(report);
                CheckSnapshot(snapshot, previousCount);
                return snapshot.ConsecutiveFailedSweeps;
            }
            catch (Exception e)
            {
                _violations.Add($"observation threw {e.GetType().Name}: {e.Message}");
                return previousCount;
            }
        }

        private void CheckGauges()
        {
            if (runningTime.Values.Any(value => value < 0)) _violations.Add("a running time gauge value was negative");
            if (trippedTopics.Values.Any(value => value < 0)) _violations.Add("a tripped topics gauge value was negative");
            if (trippedTopics.Values.Any(value => value != 0 && value != 1)) _violations.Add("a tripped topics reading was neither 0 nor the tripped count");
        }

        private void CheckReport(HealthReport report)
        {
            foreach (var (name, entry) in report.Entries)
                if (entry.Status == HealthStatus.Unhealthy) _violations.Add($"check {name} was Unhealthy: {entry.Description}");
        }

        private void CheckSnapshot(OutboxSweeperSnapshot snapshot, int previousCount)
        {
            if (!snapshot.IsSweepInProgress && snapshot.RunningTime != TimeSpan.Zero)
                _violations.Add($"not in progress but running time was {snapshot.RunningTime}");
            if (snapshot.IsSweepInProgress && snapshot.HoldsLock && !snapshot.IsActive)
                _violations.Add("in progress with the lock but not active");
            if (snapshot.IsSweepInProgress && !snapshot.HoldsLock && snapshot.IsActive)
                _violations.Add("in progress without the lock but active");
            if (snapshot.ConsecutiveFailedSweeps < previousCount && !(snapshot.ConsecutiveFailedSweeps == 0 && snapshot.LastOutcome == SweepOutcome.Completed))
                _violations.Add($"the failed count fell from {previousCount} to {snapshot.ConsecutiveFailedSweeps} without a completed sweep");
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
