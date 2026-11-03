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
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed;

public class When_reads_succeed_and_writes_fail_should_stay_in_fallback_until_a_write_succeeds
{
    private static readonly TimeSpan s_cooldown = TimeSpan.FromSeconds(50);
    private static readonly OutboxCircuitBreakerOptions s_options = new() { CooldownDuration = s_cooldown, ClockSkewLeeway = TimeSpan.Zero };
    private readonly RoutingKey _orders = new("orders");
    private readonly FakeTimeProvider _clock = new();
    private readonly WritesFailingOnDemandTrippedTopicStore _store;
    private readonly DistributedOutboxCircuitBreaker _breaker;

    public When_reads_succeed_and_writes_fail_should_stay_in_fallback_until_a_write_succeeds()
    {
        _store = new WritesFailingOnDemandTrippedTopicStore(new InMemoryTrippedTopicStore()) { FailWrites = true };
        _breaker = new DistributedOutboxCircuitBreaker(_store, s_options, timeProvider: _clock);
    }

    [Fact]
    public async Task When_an_unshared_trip_is_followed_by_successful_reads_should_stay_in_fallback()
    {
        // Arrange
        var batch = _breaker.BeginTripBatch();
        batch.TripTopic(_orders);
        await batch.WriteAsync();

        // Act
        await _breaker.ReadTrippedTopicsAsync();
        await _breaker.ReadTrippedTopicsAsync();

        // Assert
        Assert.True(_breaker.StoreStatus.IsUsingFallback);
        Assert.Equal("InvalidOperationException", _breaker.StoreStatus.LastFailure);
    }

    [Fact]
    public async Task When_a_later_write_of_the_topic_succeeds_should_end_the_fallback()
    {
        // Arrange
        var failed = _breaker.BeginTripBatch();
        failed.TripTopic(_orders);
        await failed.WriteAsync();
        await _breaker.ReadTrippedTopicsAsync();
        _store.FailWrites = false;
        _clock.Advance(TimeSpan.FromSeconds(1));

        // Act
        var succeeding = _breaker.BeginTripBatch();
        succeeding.TripTopic(_orders);
        await succeeding.WriteAsync();

        // Assert
        Assert.False(_breaker.StoreStatus.IsUsingFallback);
        Assert.Null(_breaker.StoreStatus.LastFailure);
    }

    [Fact]
    public async Task When_the_unshared_trip_ends_should_end_the_fallback()
    {
        // Arrange
        var batch = _breaker.BeginTripBatch();
        batch.TripTopic(_orders);
        await batch.WriteAsync();
        await _breaker.ReadTrippedTopicsAsync();

        // Act
        _clock.Advance(s_cooldown);
        await _breaker.ReadTrippedTopicsAsync();

        // Assert
        Assert.False(_breaker.StoreStatus.IsUsingFallback);
        Assert.Null(_breaker.StoreStatus.LastFailure);
    }
}
