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

public class BreakerLookupThrowsOnInactiveSweeperTests
{
    private const string TrippedTopics = "paramore.brighter.outbox_sweeper.tripped_topics";

    [Fact]
    public async Task When_the_breaker_lookup_throws_on_an_inactive_sweeper_should_report_tripped_topics_unreadable()
    {
        //Arrange
        var marker = $"inactive-lookup-check-{Guid.NewGuid():N}";
        var state = InactiveState();
        var check = new OutboxSweeperInvestigateHealthCheck(state, new TimedOutboxSweeperOptions(), () => throw new InvalidOperationException(marker), 3);

        //Act
        HealthCheckResult result = default;
        var exception = await Record.ExceptionAsync(async () => result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None));

        //Assert
        Assert.Null(exception);
        Assert.False(state.Read().IsActive);
        Assert.False(state.Read().IsStopped);
        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Investigate), result.Data[RemedyCheckConvention.RemedyKindKey]);
        Assert.Equal(new[] { "tripped_topics_unreadable" }, Assert.IsType<string[]>(result.Data[RemedyCheckConvention.ConditionsKey]));
        var logged = Assert.Single(CapturingLogSink.Instance.Find(typeof(OutboxSweeperInvestigateHealthCheck).FullName!, marker));
        Assert.Equal(Serilog.Events.LogEventLevel.Error, logged.Level);
        Assert.IsType<InvalidOperationException>(logged.Exception);
    }

    [Fact]
    public void When_the_breaker_lookup_throws_on_an_inactive_sweeper_should_record_zero_and_log_the_gauge_fault_once()
    {
        //Arrange
        var marker = $"inactive-lookup-gauge-{Guid.NewGuid():N}";
        var state = InactiveState();

        //Act
        var values = Observe(state, () => throw new InvalidOperationException(marker), out var recordException);

        //Assert
        Assert.Null(recordException);
        Assert.Equal([0L], values);
        var logged = Assert.Single(CapturingLogSink.Instance.Find(typeof(SweeperMeter).FullName!, marker));
        Assert.Equal(Serilog.Events.LogEventLevel.Warning, logged.Level);
    }

    [Fact]
    public async Task When_the_breaker_lookup_throws_on_a_stopped_sweeper_should_report_healthy_without_calling_the_lookup()
    {
        //Arrange
        var state = InactiveState();
        state.Stopped();
        var calls = 0;
        var check = new OutboxSweeperInvestigateHealthCheck(state, new TimedOutboxSweeperOptions(), () =>
        {
            Interlocked.Increment(ref calls);
            throw new InvalidOperationException("stopped-lookup");
        }, 3);

        //Act
        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        //Assert
        Assert.True(state.Read().IsStopped);
        Assert.Equal(0, calls);
        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    private static OutboxSweeperState InactiveState()
    {
        var state = new OutboxSweeperState(new FakeTimeProvider());
        state.SweepStarted();
        state.SweepEnded(SweepOutcome.LockUnavailable);
        return state;
    }

    private static List<long> Observe(OutboxSweeperState state, Func<IAmAnOutboxCircuitBreaker?> findCircuitBreaker, out Exception? recordException)
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

        recordException = Record.Exception(listener.RecordObservableInstruments);

        GC.KeepAlive(meter);
        return values;
    }
}
