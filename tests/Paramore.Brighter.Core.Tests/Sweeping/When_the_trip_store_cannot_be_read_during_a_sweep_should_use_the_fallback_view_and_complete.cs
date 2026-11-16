#region Licence

/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */
#endregion


using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed.TestDoubles;
using Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class SweeperStoreReadFailureTests
{
    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(10);
    private static readonly OutboxCircuitBreakerOptions s_options = new() { CooldownDuration = TimeSpan.FromSeconds(50), ClockSkewLeeway = TimeSpan.Zero };

    [Fact]
    public async Task When_the_trip_store_cannot_be_read_during_a_sweep_should_use_the_fallback_view_and_complete()
    {
        // Arrange
        var marker = $"unreadable-store-{Guid.NewGuid():N}";
        var clock = new FakeTimeProvider();
        var store = new ReadsFailingOnDemandTrippedTopicStore(new InMemoryTrippedTopicStore(), marker) { FailReads = true };
        var breaker = new DistributedOutboxCircuitBreaker(store, s_options, timeProvider: clock);
        using var outbox = new TwoTopicSweepableOutbox(breaker);
        using var measurements = new RecordedSweeperMeasurements();
        outbox.AddUndispatched(outbox.Orders);
        outbox.AddUndispatched(outbox.Invoices);
        breaker.TripTopic(outbox.Orders);
        var sweeper = NewSweeper(outbox, measurements, clock);

        // Act
        await sweeper.StartAsync(CancellationToken.None);
        var swept = measurements.WaitFor(m => m.Of(RecordedSweeperMeasurements.Sweeps).Count == 1, s_timeout);
        await sweeper.StopAsync(CancellationToken.None);
        sweeper.Dispose();

        // Assert
        Assert.True(swept, "The sweeper recorded no sweep");
        Assert.Equal("completed", measurements.Of(RecordedSweeperMeasurements.Sweeps).Single().Outcome);
        Assert.False(outbox.HasPublishedTo(outbox.Orders));
        Assert.True(outbox.HasPublishedTo(outbox.Invoices));
        Assert.True(breaker.StoreStatus.IsUsingFallback);
        var warning = Assert.Single(CapturingLogSink.Instance.Find(typeof(DistributedOutboxCircuitBreaker).FullName!, marker));
        Assert.Equal(Serilog.Events.LogEventLevel.Warning, warning.Level);
    }

    [Fact]
    public async Task When_a_breaker_has_never_read_the_store_during_a_sweep_should_leave_out_no_topic()
    {
        // Arrange
        var marker = $"never-read-store-{Guid.NewGuid():N}";
        var clock = new FakeTimeProvider();
        var store = new ReadsFailingOnDemandTrippedTopicStore(new InMemoryTrippedTopicStore(), marker) { FailReads = true };
        var breaker = new DistributedOutboxCircuitBreaker(store, s_options, timeProvider: clock);
        using var outbox = new TwoTopicSweepableOutbox(breaker);
        using var measurements = new RecordedSweeperMeasurements();
        outbox.AddUndispatched(outbox.Orders);
        outbox.AddUndispatched(outbox.Invoices);
        var sweeper = NewSweeper(outbox, measurements, clock);

        // Act
        await sweeper.StartAsync(CancellationToken.None);
        var swept = measurements.WaitFor(m => m.Of(RecordedSweeperMeasurements.Sweeps).Count == 1, s_timeout);
        await sweeper.StopAsync(CancellationToken.None);
        sweeper.Dispose();

        // Assert
        Assert.True(swept, "The sweeper recorded no sweep");
        Assert.Equal("completed", measurements.Of(RecordedSweeperMeasurements.Sweeps).Single().Outcome);
        Assert.True(outbox.HasPublishedTo(outbox.Orders));
        Assert.True(outbox.HasPublishedTo(outbox.Invoices));
    }

    [Fact]
    public async Task When_the_store_answers_with_a_trip_after_a_failed_read_should_exclude_the_topic_from_the_next_sweep()
    {
        // Arrange
        var marker = $"recovering-store-{Guid.NewGuid():N}";
        var clock = new FakeTimeProvider();
        var shared = new InMemoryTrippedTopicStore();
        var store = new ReadsFailingOnDemandTrippedTopicStore(shared, marker) { FailReads = true };
        var breaker = new DistributedOutboxCircuitBreaker(store, s_options, timeProvider: clock);
        var other = new DistributedOutboxCircuitBreaker(shared, s_options, timeProvider: clock);
        using var outbox = new TwoTopicSweepableOutbox(breaker);
        using var measurements = new RecordedSweeperMeasurements();
        outbox.AddUndispatched(outbox.Orders);
        outbox.AddUndispatched(outbox.Invoices);
        other.TripTopic(outbox.Orders);
        var tripShared = SpinWait.SpinUntil(() => IsShared(shared, clock), s_timeout);
        var sweeper = NewSweeper(outbox, measurements, clock);

        // Act
        await sweeper.StartAsync(CancellationToken.None);
        var sweptDuringOutage = measurements.WaitFor(m => m.Of(RecordedSweeperMeasurements.Sweeps).Count == 1, s_timeout);
        var orderDispatchedDuringOutage = outbox.HasPublishedTo(outbox.Orders);
        outbox.AddUndispatched(outbox.Orders);
        store.FailReads = false;
        clock.Advance(TimeSpan.FromSeconds(5));
        var sweptAfterRecovery = measurements.WaitFor(m => m.Of(RecordedSweeperMeasurements.Sweeps).Count == 2, s_timeout);
        await sweeper.StopAsync(CancellationToken.None);
        sweeper.Dispose();

        // Assert
        Assert.True(tripShared, "The other breaker did not share its trip");
        Assert.True(sweptDuringOutage && sweptAfterRecovery, "The sweeps did not both complete");
        Assert.True(orderDispatchedDuringOutage);
        Assert.False(breaker.StoreStatus.IsUsingFallback);
        Assert.Equal([outbox.Orders], breaker.TrippedTopics.ToArray());
        Assert.Single(outbox.Bus.Stream(outbox.Orders));
    }

    private static bool IsShared(InMemoryTrippedTopicStore store, FakeTimeProvider clock)
        => store.ReadLiveAsync(new TripWindow(clock.GetUtcNow(), s_options.CooldownDuration)).GetAwaiter().GetResult().Count > 0;

    private static TimedOutboxSweeper NewSweeper(TwoTopicSweepableOutbox outbox, RecordedSweeperMeasurements measurements, FakeTimeProvider clock)
        => new(
            outbox.ScopeFactory,
            new InMemoryLock(),
            new TimedOutboxSweeperOptions { TimerInterval = 5, MinimumMessageAge = TimeSpan.Zero },
            timeProvider: clock,
            meter: new SweeperMeter(measurements.MeterFactory));
}
