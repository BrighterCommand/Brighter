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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;
using Paramore.Brighter.Outbox.Hosting.HealthChecks;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class TripStoreBlockedHealthSignalLatencyTests
{
    private const string TrippedTopicsGauge = "paramore.brighter.outbox_sweeper.tripped_topics";
    private static readonly TimeSpan s_withinASecond = TimeSpan.FromSeconds(1);

    [Fact]
    public async Task When_the_trip_store_blocks_indefinitely_should_complete_the_gauge_within_a_second()
    {
        //Arrange
        var state = ActiveState();
        var breaker = BreakerWithBlockedStore();
        using var metrics = new ServiceCollection().AddMetrics().BuildServiceProvider();
        var meterFactory = metrics.GetRequiredService<IMeterFactory>();
        var meter = new SweeperMeter(meterFactory, state, () => breaker);
        var values = new List<long>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (!ReferenceEquals(instrument.Meter.Scope, meterFactory) || instrument.Name != TrippedTopicsGauge) return;
            l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, value, _, _) => values.Add(value));
        listener.Start();

        //Act
        await Task.Run(() => listener.RecordObservableInstruments()).WaitAsync(s_withinASecond);

        //Assert
        Assert.Equal([1L], values);
        GC.KeepAlive(meter);
    }

    [Fact]
    public async Task When_the_trip_store_blocks_indefinitely_should_complete_the_check_within_a_second()
    {
        //Arrange
        var state = ActiveState();
        var breaker = BreakerWithBlockedStore();
        var check = new OutboxSweeperInvestigateHealthCheck(state, new TimedOutboxSweeperOptions(), () => breaker, 3);

        //Act
        var result = await Task.Run(() => check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None))
            .WaitAsync(s_withinASecond);

        //Assert
        var tripped = Assert.IsAssignableFrom<IDictionary<string, object>>(result.Data["tripped_topics"]);
        Assert.Equal(new[] { "orders" }, Assert.IsType<string[]>(tripped["topics"]));
        Assert.Equal(1, tripped["count"]);
    }

    private static OutboxSweeperState ActiveState()
    {
        var state = new OutboxSweeperState(new FakeTimeProvider());
        state.SweepStarted();
        state.SweepEnded(SweepOutcome.LockUnavailable);
        return state;
    }

    private static DistributedOutboxCircuitBreaker BreakerWithBlockedStore()
    {
        var breaker = new DistributedOutboxCircuitBreaker(new NeverCompletingTrippedTopicStore(), new OutboxCircuitBreakerOptions { StoreTimeout = TimeSpan.FromSeconds(2) });
        breaker.TripTopic(new RoutingKey("orders"));
        breaker.StartRefresh();
        return breaker;
    }
}
