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
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.Metrics;

public class SharedBreakerTrippedTopicsGaugeOnInactiveSweeperTests
{
    private const string TrippedTopics = "paramore.brighter.outbox_sweeper.tripped_topics";
    private static readonly OutboxCircuitBreakerOptions s_options = new() { CooldownDuration = TimeSpan.FromSeconds(50), ClockSkewLeeway = TimeSpan.Zero };

    [Fact]
    public async Task When_the_breaker_shares_trips_should_report_the_tripped_topics_gauge_when_the_last_sweep_could_not_get_the_lock()
    {
        //Arrange
        var state = new OutboxSweeperState(new FakeTimeProvider());
        state.SweepStarted();
        state.SweepEnded(SweepOutcome.LockUnavailable);
        var breakerB = await BreakerSeeingTripsFromAnotherInstance();

        //Act
        var values = Observe(state, () => breakerB);

        //Assert
        Assert.Equal([2L], values);
    }

    [Fact]
    public async Task When_the_breaker_shares_trips_should_report_the_tripped_topics_gauge_while_obtaining_the_lock()
    {
        //Arrange
        var state = new OutboxSweeperState(new FakeTimeProvider());
        state.SweepStarted();
        state.SweepEnded(SweepOutcome.LockUnavailable);
        state.SweepStarted();
        var breakerB = await BreakerSeeingTripsFromAnotherInstance();

        //Act
        var values = Observe(state, () => breakerB);

        //Assert
        Assert.Equal([2L], values);
    }

    [Fact]
    public async Task When_the_breaker_shares_trips_should_report_zero_on_a_stopped_sweeper()
    {
        //Arrange
        var state = new OutboxSweeperState(new FakeTimeProvider());
        state.SweepStarted();
        state.SweepEnded(SweepOutcome.LockUnavailable);
        state.Stopped();
        var breakerB = await BreakerSeeingTripsFromAnotherInstance();

        //Act
        var values = Observe(state, () => breakerB);

        //Assert
        Assert.Equal([0L], values);
    }

    [Fact]
    public async Task When_the_breaker_shares_trips_should_report_zero_without_a_sweeper_record()
    {
        //Arrange
        var breakerB = await BreakerSeeingTripsFromAnotherInstance();

        //Act
        var values = Observe(null, () => breakerB);

        //Assert
        Assert.Equal([0L], values);
    }

    [Fact]
    public void When_the_breaker_shares_trips_should_report_only_its_own_trips_before_any_read_has_succeeded()
    {
        //Arrange
        var state = new OutboxSweeperState(new FakeTimeProvider());
        var breaker = new DistributedOutboxCircuitBreaker(new InMemoryTrippedTopicStore(), s_options);

        //Act
        var before = Observe(state, () => breaker);
        breaker.TripTopic(new RoutingKey("orders"));
        var after = Observe(state, () => breaker);

        //Assert
        Assert.Equal([0L], before);
        Assert.Equal([1L], after);
    }

    private static async Task<DistributedOutboxCircuitBreaker> BreakerSeeingTripsFromAnotherInstance()
    {
        var clock = new FakeTimeProvider();
        var store = new InMemoryTrippedTopicStore();
        var breakerA = new DistributedOutboxCircuitBreaker(store, s_options, timeProvider: clock);
        var breakerB = new DistributedOutboxCircuitBreaker(store, s_options, timeProvider: clock);
        var batch = breakerA.BeginTripBatch();
        batch.TripTopic(new RoutingKey("orders"));
        batch.TripTopic(new RoutingKey("invoices"));
        await batch.WriteAsync();
        await breakerB.ReadTrippedTopicsAsync();
        return breakerB;
    }

    private static List<long> Observe(OutboxSweeperState? state, Func<IAmAnOutboxCircuitBreaker?> findCircuitBreaker)
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
