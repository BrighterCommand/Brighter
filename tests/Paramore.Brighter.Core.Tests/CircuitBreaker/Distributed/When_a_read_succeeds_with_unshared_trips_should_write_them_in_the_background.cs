#region License

/* The MIT License (MIT)
Copyright © 2025 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed;

public class When_a_read_succeeds_with_unshared_trips_should_write_them_in_the_background
{
    private static readonly OutboxCircuitBreakerOptions s_options = new() { CooldownDuration = TimeSpan.FromSeconds(50), ClockSkewLeeway = TimeSpan.Zero };
    private static readonly TimeSpan s_patience = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan s_settle = TimeSpan.FromMilliseconds(200);
    private readonly RoutingKey _orders = new("orders");
    private readonly RoutingKey _payments = new("payments");
    private readonly FakeTimeProvider _clock = new();
    private readonly InMemoryTrippedTopicStore _shared = new();

    private async Task<IOrderedEnumerable<string>> TopicsSharedAsync()
    {
        var live = await _shared.ReadLiveAsync(new TripWindow(_clock.GetUtcNow(), s_options.CooldownDuration));
        return live.Select(trip => trip.Topic.Value).OrderBy(topic => topic);
    }

    private async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + s_patience;
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(5);
    }

    private async Task WaitForSharedTopicsAsync(int count)
    {
        var deadline = DateTime.UtcNow + s_patience;
        while ((await TopicsSharedAsync()).Count() < count && DateTime.UtcNow < deadline)
            await Task.Delay(5);
    }

    [Fact]
    public async Task When_a_read_succeeds_after_a_write_failure_should_write_the_unshared_trip_without_the_read_awaiting_it()
    {
        // Arrange
        var recorded = new SignallingTrippedTopicStore(_shared, WriteHold.AllWrites);
        var store = new WritesFailingOnDemandTrippedTopicStore(recorded) { FailWrites = true };
        var breaker = new DistributedOutboxCircuitBreaker(store, s_options, timeProvider: _clock);
        var batch = breaker.BeginTripBatch();
        batch.TripTopic(_orders);
        await batch.WriteAsync();
        store.FailWrites = false;

        // Act
        await breaker.ReadTrippedTopicsAsync().WaitAsync(s_patience);
        await recorded.WriteStarted.WaitAsync(s_patience);
        var sharedWhileHeld = await TopicsSharedAsync();
        recorded.ReleaseWrites();
        await recorded.WriteCompleted.WaitAsync(s_patience);

        // Assert
        Assert.Empty(sharedWhileHeld);
        Assert.Equal(["orders"], await TopicsSharedAsync());
        Assert.Single(recorded.WrittenTrips);
        var other = new DistributedOutboxCircuitBreaker(_shared, s_options, timeProvider: _clock);
        Assert.Equal([_orders], await other.ReadTrippedTopicsAsync());
    }

    [Fact]
    public async Task When_a_request_only_instance_trips_another_topic_should_write_the_unshared_trip_with_it()
    {
        // Arrange
        var store = new WritesFailingOnDemandTrippedTopicStore(_shared) { FailWrites = true };
        var breaker = new DistributedOutboxCircuitBreaker(store, s_options, timeProvider: _clock);
        breaker.TripTopic(_orders);
        await WaitUntilAsync(() => breaker.StoreStatus.IsUsingFallback);
        store.FailWrites = false;

        // Act
        breaker.TripTopic(_payments);
        await WaitForSharedTopicsAsync(2);

        // Assert
        Assert.Equal(["orders", "payments"], await TopicsSharedAsync());
        await WaitUntilAsync(() => !breaker.StoreStatus.IsUsingFallback);
        Assert.False(breaker.StoreStatus.IsUsingFallback);
    }

    [Fact]
    public async Task When_reads_succeed_while_a_recovery_write_is_running_should_run_only_one_recovery_write()
    {
        // Arrange
        var recorded = new SignallingTrippedTopicStore(_shared, WriteHold.AllWrites);
        var store = new WritesFailingOnDemandTrippedTopicStore(recorded) { FailWrites = true };
        var breaker = new DistributedOutboxCircuitBreaker(store, s_options, timeProvider: _clock);
        var batch = breaker.BeginTripBatch();
        batch.TripTopic(_orders);
        await batch.WriteAsync();
        store.FailWrites = false;
        await breaker.ReadTrippedTopicsAsync().WaitAsync(s_patience);
        await recorded.WriteStarted.WaitAsync(s_patience);

        // Act
        await breaker.ReadTrippedTopicsAsync().WaitAsync(s_patience);
        await breaker.ReadTrippedTopicsAsync().WaitAsync(s_patience);
        var secondStarted = await Task.WhenAny(recorded.SecondWriteStarted, Task.Delay(s_settle)) == recorded.SecondWriteStarted;
        recorded.ReleaseWrites();
        await recorded.WriteCompleted.WaitAsync(s_patience);

        // Assert
        Assert.False(secondStarted);
        Assert.Single(recorded.WrittenTrips);
    }
}
