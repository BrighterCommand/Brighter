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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.Metrics;

public class When_an_active_sweeper_has_topics_tripped_should_report_the_tripped_topics_gauge
{
    private const string TrippedTopics = "paramore.brighter.outbox_sweeper.tripped_topics";

    [Fact]
    public void When_two_topics_are_tripped_should_record_two()
    {
        //Arrange
        var state = ActiveState();
        var breaker = new InMemoryOutboxCircuitBreaker();
        breaker.TripTopic(new RoutingKey("topic-one"));
        breaker.TripTopic(new RoutingKey("topic-two"));

        //Act
        var observed = Observe(state, () => breaker);

        //Assert
        Assert.Equal([2L], observed.Values);
        Assert.Equal("{topic}", observed.Unit);
        Assert.Equal(0, observed.TagCount);
    }

    [Fact]
    public void When_an_active_sweeper_has_no_topics_tripped_should_report_a_zero_tripped_topics_gauge()
    {
        //Arrange
        var state = ActiveState();
        var breaker = new InMemoryOutboxCircuitBreaker();

        //Act
        var observed = Observe(state, () => breaker);

        //Assert
        Assert.Equal([0L], observed.Values);
    }

    [Fact]
    public void When_an_active_sweeper_has_no_circuit_breaker_should_report_a_zero_tripped_topics_gauge()
    {
        //Arrange
        var state = ActiveState();

        //Act
        var observed = Observe(state, () => null);

        //Assert
        Assert.Equal([0L], observed.Values);
    }

    [Fact]
    public void When_the_meter_has_no_state_should_report_a_zero_tripped_topics_gauge_even_if_topics_are_tripped()
    {
        //Arrange
        var breaker = new InMemoryOutboxCircuitBreaker();
        breaker.TripTopic(new RoutingKey("topic-one"));

        //Act
        var observed = Observe(null, () => breaker);

        //Assert
        Assert.Equal([0L], observed.Values);
    }

    private static OutboxSweeperState ActiveState()
    {
        var state = new OutboxSweeperState(new FakeTimeProvider());
        state.SweepStarted();
        state.LockObtained();
        state.SweepEnded(SweepOutcome.Completed);
        return state;
    }

    private static Observation Observe(OutboxSweeperState? state, Func<IAmAnOutboxCircuitBreaker?> findCircuitBreaker)
    {
        using var metrics = new ServiceCollection().AddMetrics().BuildServiceProvider();
        var meterFactory = metrics.GetRequiredService<IMeterFactory>();
        var meter = new SweeperMeter(meterFactory, state, findCircuitBreaker);
        var observation = new Observation();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (!ReferenceEquals(instrument.Meter.Scope, meterFactory) || instrument.Name != TrippedTopics) return;
            observation.Unit = instrument.Unit;
            l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            observation.TagCount = tags.Length;
            observation.Values.Add(value);
        });
        listener.Start();

        listener.RecordObservableInstruments();

        GC.KeepAlive(meter);
        return observation;
    }

    private sealed class Observation
    {
        public string? Unit { get; set; }
        public int TagCount { get; set; }
        public List<long> Values { get; } = [];
    }
}
