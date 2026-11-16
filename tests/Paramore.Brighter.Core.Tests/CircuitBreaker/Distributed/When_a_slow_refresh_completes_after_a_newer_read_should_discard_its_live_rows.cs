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
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed.TestDoubles;
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed;

public class When_a_slow_refresh_completes_after_a_newer_read_should_discard_its_live_rows
{
    private static readonly OutboxCircuitBreakerOptions s_options = new() { CooldownDuration = TimeSpan.FromSeconds(50), ClockSkewLeeway = TimeSpan.Zero };
    private readonly RoutingKey _orders = new("orders");
    private readonly RoutingKey _billing = new("billing");
    private readonly RoutingKey _shipping = new("shipping");
    private readonly FakeTimeProvider _clock = new();
    private readonly IndividuallyHeldReadsTrippedTopicStore _store = new(new InMemoryTrippedTopicStore());
    private readonly DistributedOutboxCircuitBreaker _breaker;

    public When_a_slow_refresh_completes_after_a_newer_read_should_discard_its_live_rows()
    {
        _breaker = new DistributedOutboxCircuitBreaker(_store, s_options, timeProvider: _clock);
    }

    private static async Task<bool> BecomesTrueAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(10);

        return condition();
    }

    private static int ResetCountFor(string topic)
        => TestCorrelator.GetLogEventsFromCurrentContext()
            .Count(e => e.Level == LogEventLevel.Information && e.RenderMessage().Contains($"reset for topic \"{topic}\""));

    private async Task StartSlowRefreshAsync()
    {
        _breaker.StartRefresh();
        await _store.ReadStarted(1);
    }

    private async Task CompleteNewerSweepReadAsync(HeldRead read)
    {
        var sweep = _breaker.ReadTrippedTopicsAsync();
        await _store.ReadStarted(2);
        _store.CompleteRead(2, read);
        await sweep;
    }

    [Fact]
    public async Task When_a_slow_refresh_completes_after_a_newer_sweep_read_should_discard_its_live_rows_and_announce_its_ended_rows()
    {
        // Arrange
        using var context = TestCorrelator.CreateContext();
        var now = _clock.GetUtcNow();
        await StartSlowRefreshAsync();
        await CompleteNewerSweepReadAsync(new HeldRead([new TopicTrip(_orders, now)], []));

        // Act
        _store.CompleteRead(1, new HeldRead([new TopicTrip(_billing, now)], [new TopicTrip(_shipping, now.AddSeconds(-60))]));
        await BecomesTrueAsync(() => ResetCountFor("shipping") > 0);
        await Task.Delay(100);

        // Assert
        Assert.Equal([_orders], _breaker.TrippedTopics);
        Assert.Equal(1, ResetCountFor("shipping"));
    }

    [Fact]
    public async Task When_a_slow_read_started_before_a_write_completes_after_it_should_keep_the_written_trip()
    {
        // Arrange
        await StartSlowRefreshAsync();
        var batch = _breaker.BeginTripBatch();
        batch.TripTopic(_orders);
        await batch.WriteAsync();

        // Act
        _store.CompleteRead(1, new HeldRead([], [new TopicTrip(_orders, _clock.GetUtcNow().AddSeconds(-1))]));
        await Task.Delay(200);

        // Assert
        Assert.Equal([_orders], _breaker.TrippedTopics);
    }

    [Fact]
    public async Task When_a_stale_read_fails_should_change_neither_the_fallback_state_nor_the_last_failure()
    {
        // Arrange
        await StartSlowRefreshAsync();
        await CompleteNewerSweepReadAsync(new HeldRead([], []));

        // Act
        _store.FailRead(1, new InvalidOperationException("The store cannot be read"));
        await Task.Delay(200);

        // Assert
        Assert.False(_breaker.StoreStatus.IsUsingFallback);
        Assert.Null(_breaker.StoreStatus.LastFailure);
    }

    [Fact]
    public async Task When_a_slow_read_succeeds_after_a_newer_read_failed_should_stay_in_fallback_with_the_newer_failure()
    {
        // Arrange
        await StartSlowRefreshAsync();
        var sweep = _breaker.ReadTrippedTopicsAsync();
        await _store.ReadStarted(2);
        _store.FailRead(2, new InvalidOperationException("The store cannot be read"));
        await sweep;

        // Act
        _store.CompleteRead(1, new HeldRead([], []));
        await Task.Delay(200);

        // Assert
        Assert.True(_breaker.StoreStatus.IsUsingFallback);
        Assert.Equal("InvalidOperationException", _breaker.StoreStatus.LastFailure);
    }
}
