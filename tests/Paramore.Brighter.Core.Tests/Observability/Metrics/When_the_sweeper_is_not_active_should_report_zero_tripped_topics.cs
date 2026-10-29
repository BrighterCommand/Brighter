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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.Metrics;

public class InactiveSweeperZeroTrippedTopicsGaugeTests
{
    private const string TrippedTopics = "paramore.brighter.outbox_sweeper.tripped_topics";

    [Fact]
    public void When_the_sweeper_is_not_active_should_report_zero_tripped_topics_before_it_has_ended_a_sweep()
    {
        //Arrange
        var state = new OutboxSweeperState(new FakeTimeProvider());
        state.SweepStarted();

        //Act
        var values = ObserveWithOrdersTripped(state);

        //Assert
        Assert.Equal([0L], values);
    }

    [Fact]
    public void When_the_sweeper_is_not_active_should_report_zero_tripped_topics_when_the_last_sweep_could_not_get_the_lock()
    {
        //Arrange
        var state = new OutboxSweeperState(new FakeTimeProvider());
        state.SweepStarted();
        state.SweepEnded(SweepOutcome.LockUnavailable);

        //Act
        var values = ObserveWithOrdersTripped(state);

        //Assert
        Assert.Equal([0L], values);
    }

    [Fact]
    public void When_the_sweeper_is_not_active_should_report_zero_tripped_topics_while_acquiring_the_lock_after_a_completed_sweep()
    {
        //Arrange
        var state = new OutboxSweeperState(new FakeTimeProvider());
        state.SweepStarted();
        state.LockObtained();
        state.SweepEnded(SweepOutcome.Completed);
        state.SweepStarted();

        //Act
        var values = ObserveWithOrdersTripped(state);

        //Assert
        Assert.Equal([0L], values);
    }

    [Fact]
    public void When_the_sweeper_is_not_active_should_report_zero_tripped_topics_when_the_last_sweep_failed_without_obtaining_the_lock()
    {
        //Arrange
        var state = new OutboxSweeperState(new FakeTimeProvider());
        state.SweepStarted();
        state.SweepEnded(SweepOutcome.Failed);

        //Act
        var values = ObserveWithOrdersTripped(state);

        //Assert
        Assert.Equal([0L], values);
    }

    [Fact]
    public void When_the_sweeper_is_not_active_should_report_zero_tripped_topics_when_stopped_after_a_completed_sweep()
    {
        //Arrange
        var state = new OutboxSweeperState(new FakeTimeProvider());
        state.SweepStarted();
        state.LockObtained();
        state.SweepEnded(SweepOutcome.Completed);
        state.Stopped();

        //Act
        var values = ObserveWithOrdersTripped(state);

        //Assert
        Assert.Equal([0L], values);
    }

    [Fact]
    public void When_the_sweeper_is_not_active_should_report_zero_tripped_topics_without_reading_the_circuit_breaker()
    {
        //Arrange
        var marker = $"tripped-topics-{Guid.NewGuid():N}";
        var state = new OutboxSweeperState(new FakeTimeProvider());
        state.SweepStarted();
        state.SweepEnded(SweepOutcome.LockUnavailable);

        //Act
        var values = Observe(state, () => new ThrowingOutboxCircuitBreaker(marker));

        //Assert
        Assert.Equal([0L], values);
        Assert.Empty(CapturingLogSink.Instance.Find(typeof(SweeperMeter).FullName!, marker));
    }

    private static List<long> ObserveWithOrdersTripped(OutboxSweeperState state)
    {
        var breaker = new InMemoryOutboxCircuitBreaker();
        breaker.TripTopic(new RoutingKey("orders"));
        return Observe(state, () => breaker);
    }

    private static List<long> Observe(OutboxSweeperState state, Func<IAmAnOutboxCircuitBreaker?> findCircuitBreaker)
    {
        using var metrics = new ServiceCollection().AddMetrics().BuildServiceProvider();
        var meterFactory = metrics.GetRequiredService<IMeterFactory>();
        var meter = new SweeperMeter(meterFactory, state, findCircuitBreaker);
        var values = new List<long>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (!ReferenceEquals(instrument.Meter.Scope, meterFactory) || instrument.Name != TrippedTopics) return;
            l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, value, _, _) => values.Add(value));
        listener.Start();

        listener.RecordObservableInstruments();

        GC.KeepAlive(meter);
        return values;
    }
}
