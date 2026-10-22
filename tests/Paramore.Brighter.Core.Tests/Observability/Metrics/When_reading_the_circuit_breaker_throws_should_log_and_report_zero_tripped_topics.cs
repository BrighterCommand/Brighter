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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.Metrics;

public class When_reading_the_circuit_breaker_throws_should_log_and_report_zero_tripped_topics
{
    private const string TrippedTopics = "paramore.brighter.outbox_sweeper.tripped_topics";
    private const string RunningTime = "paramore.brighter.outbox_sweeper.sweep.running_time";

    [Fact]
    public void When_the_breaker_read_throws_should_log_a_warning_and_report_zero_tripped_topics()
    {
        //Arrange
        var marker = $"breaker-read-{Guid.NewGuid():N}";
        var timeProvider = new FakeTimeProvider();
        var state = ActiveStateRunningFor(timeProvider, TimeSpan.FromSeconds(5));

        //Act
        var observed = Observe(state, () => new ThrowingOutboxCircuitBreaker(marker), out var recordException);

        //Assert
        Assert.Null(recordException);
        Assert.Equal([0.0], observed[TrippedTopics]);
        Assert.Equal([5.0], observed[RunningTime]);
        var logged = Assert.Single(CapturingLogSink.Instance.Find(typeof(SweeperMeter).FullName!, marker));
        Assert.Equal(Serilog.Events.LogEventLevel.Warning, logged.Level);
    }

    [Fact]
    public void When_the_circuit_breaker_lookup_throws_should_log_and_report_zero_tripped_topics()
    {
        //Arrange
        var marker = $"breaker-lookup-{Guid.NewGuid():N}";
        var timeProvider = new FakeTimeProvider();
        var state = ActiveStateRunningFor(timeProvider, TimeSpan.FromSeconds(5));

        //Act
        var observed = Observe(
            state, () => throw new InvalidOperationException(marker), out var recordException);

        //Assert
        Assert.Null(recordException);
        Assert.Equal([0.0], observed[TrippedTopics]);
        Assert.Equal([5.0], observed[RunningTime]);
        var logged = Assert.Single(CapturingLogSink.Instance.Find(typeof(SweeperMeter).FullName!, marker));
        Assert.Equal(Serilog.Events.LogEventLevel.Warning, logged.Level);
    }

    [Fact]
    public async Task When_the_breaker_read_throws_should_keep_sweeping()
    {
        //Arrange
        var marker = $"breaker-sweeper-{Guid.NewGuid():N}";
        using var outbox = new SweepableOutbox();
        using var measurements = new RecordedSweeperMeasurements();
        var state = new OutboxSweeperState(TimeProvider.System);
        var meter = new SweeperMeter(measurements.MeterFactory, state, () => new ThrowingOutboxCircuitBreaker(marker));
        var sweeper = new TimedOutboxSweeper(
            outbox.ScopeFactory,
            new InMemoryLock(),
            new TimedOutboxSweeperOptions { TimerInterval = 1, MinimumMessageAge = TimeSpan.Zero },
            meter: meter,
            state: state);

        //Act
        Exception? recordException = null;
        bool laterSweepCompleted;
        await sweeper.StartAsync(CancellationToken.None);
        try
        {
            var firstSweep = measurements.WaitFor(m => m.Of(BrighterSemanticConventions.OutboxSweeperSweeps).Count >= 1, TimeSpan.FromSeconds(10));
            Assert.True(firstSweep, "The first sweep did not complete within 10s");
            recordException = Record.Exception(measurements.RecordObservableInstruments);
            laterSweepCompleted = measurements.WaitFor(m => m.Of(BrighterSemanticConventions.OutboxSweeperSweeps).Count >= 3, TimeSpan.FromSeconds(10));
        }
        finally
        {
            await sweeper.StopAsync(new CancellationToken(canceled: true));
            sweeper.Dispose();
        }

        //Assert
        Assert.Null(recordException);
        Assert.True(laterSweepCompleted, "The sweeper did not complete a later sweep after the breaker read threw");
        Assert.NotEmpty(CapturingLogSink.Instance.Find(typeof(SweeperMeter).FullName!, marker));
    }

    private static OutboxSweeperState ActiveStateRunningFor(FakeTimeProvider timeProvider, TimeSpan runningFor)
    {
        var state = new OutboxSweeperState(timeProvider);
        state.SweepStarted();
        state.LockObtained();
        state.SweepEnded(SweepOutcome.Completed);
        state.SweepStarted();
        state.LockObtained();
        timeProvider.Advance(runningFor);
        return state;
    }

    private static Dictionary<string, List<double>> Observe(
        OutboxSweeperState state, Func<IAmAnOutboxCircuitBreaker?> findCircuitBreaker, out Exception? recordException)
    {
        using var metrics = new ServiceCollection().AddMetrics().BuildServiceProvider();
        var meterFactory = metrics.GetRequiredService<IMeterFactory>();
        var meter = new SweeperMeter(meterFactory, state, findCircuitBreaker);
        var values = new Dictionary<string, List<double>>
        {
            [TrippedTopics] = [],
            [RunningTime] = []
        };
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (!ReferenceEquals(instrument.Meter.Scope, meterFactory) || !values.ContainsKey(instrument.Name)) return;
            l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, _, _) => values[instrument.Name].Add(value));
        listener.SetMeasurementEventCallback<double>((instrument, value, _, _) => values[instrument.Name].Add(value));
        listener.Start();

        recordException = Record.Exception(listener.RecordObservableInstruments);

        GC.KeepAlive(meter);
        return values;
    }
}
