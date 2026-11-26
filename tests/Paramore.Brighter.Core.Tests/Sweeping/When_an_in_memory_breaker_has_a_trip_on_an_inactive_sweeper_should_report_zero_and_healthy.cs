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
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;
using Paramore.Brighter.Outbox.Hosting.HealthChecks;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class InMemoryBreakerTripOnInactiveSweeperTests
{
    private const string TrippedTopics = "paramore.brighter.outbox_sweeper.tripped_topics";

    [Fact]
    public void When_an_in_memory_breaker_has_a_trip_on_an_inactive_sweeper_should_report_zero_on_the_gauge()
    {
        //Arrange
        var state = InactiveSweeperState();
        var breaker = new InMemoryOutboxCircuitBreaker();
        breaker.TripTopic(new RoutingKey("orders"));

        //Act
        var values = ObserveGauge(state, breaker);

        //Assert
        Assert.Equal([0L], values);
    }

    [Fact]
    public async Task When_an_in_memory_breaker_has_a_trip_on_an_inactive_sweeper_should_report_healthy()
    {
        //Arrange
        var state = InactiveSweeperState();
        var breaker = new InMemoryOutboxCircuitBreaker();
        breaker.TripTopic(new RoutingKey("orders"));

        //Act
        var result = await Check(state, breaker);

        //Assert
        Assert.False(state.Read().IsActive);
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Empty(Assert.IsType<string[]>(result.Data[RemedyCheckConvention.ConditionsKey]));
    }

    [Fact]
    public void When_a_third_party_breaker_has_a_trip_on_an_inactive_sweeper_should_report_zero_on_the_gauge()
    {
        //Arrange
        var state = InactiveSweeperState();
        var breaker = new ListedTopicsCircuitBreaker("orders");

        //Act
        var values = ObserveGauge(state, breaker);

        //Assert
        Assert.Equal([0L], values);
    }

    [Fact]
    public async Task When_a_third_party_breaker_has_a_trip_on_an_inactive_sweeper_should_report_healthy()
    {
        //Arrange
        var state = InactiveSweeperState();
        var breaker = new ListedTopicsCircuitBreaker("orders");

        //Act
        var result = await Check(state, breaker);

        //Assert
        Assert.False(state.Read().IsActive);
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Empty(Assert.IsType<string[]>(result.Data[RemedyCheckConvention.ConditionsKey]));
    }

    private static OutboxSweeperState InactiveSweeperState()
    {
        var state = new OutboxSweeperState(new FakeTimeProvider());
        state.SweepStarted();
        state.SweepEnded(SweepOutcome.LockUnavailable);
        return state;
    }

    private static Task<HealthCheckResult> Check(OutboxSweeperState state, IAmAnOutboxCircuitBreaker breaker)
    {
        var check = new OutboxSweeperInvestigateHealthCheck(state, new TimedOutboxSweeperOptions(), () => breaker, 3);
        return check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);
    }

    private static List<long> ObserveGauge(OutboxSweeperState state, IAmAnOutboxCircuitBreaker breaker)
    {
        using var metrics = new ServiceCollection().AddMetrics().BuildServiceProvider();
        var meterFactory = metrics.GetRequiredService<IMeterFactory>();
        var meter = new SweeperMeter(meterFactory, state, () => breaker);
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
