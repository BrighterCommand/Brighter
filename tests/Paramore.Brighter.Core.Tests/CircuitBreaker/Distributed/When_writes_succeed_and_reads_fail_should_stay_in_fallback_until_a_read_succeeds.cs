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

public class When_writes_succeed_and_reads_fail_should_stay_in_fallback_until_a_read_succeeds
{
    private static readonly OutboxCircuitBreakerOptions s_options = new() { CooldownDuration = TimeSpan.FromSeconds(50), ClockSkewLeeway = TimeSpan.Zero };
    private readonly RoutingKey _orders = new("orders");
    private readonly FakeTimeProvider _clock = new();
    private readonly ReadsFailingOnDemandTrippedTopicStore _store;
    private readonly DistributedOutboxCircuitBreaker _breaker;

    public When_writes_succeed_and_reads_fail_should_stay_in_fallback_until_a_read_succeeds()
    {
        _store = new ReadsFailingOnDemandTrippedTopicStore(new InMemoryTrippedTopicStore()) { FailReads = true };
        _breaker = new DistributedOutboxCircuitBreaker(_store, s_options, timeProvider: _clock);
    }

    [Fact]
    public async Task When_a_write_succeeds_after_a_failed_read_should_stay_in_fallback()
    {
        // Arrange
        await _breaker.ReadTrippedTopicsAsync();
        Assert.True(_breaker.StoreStatus.IsUsingFallback);
        var failure = _breaker.StoreStatus.LastFailure;

        // Act
        var batch = _breaker.BeginTripBatch();
        batch.TripTopic(_orders);
        await batch.WriteAsync();

        // Assert
        Assert.True(_breaker.StoreStatus.IsUsingFallback);
        Assert.Equal(failure, _breaker.StoreStatus.LastFailure);
    }

    [Fact]
    public async Task When_a_read_then_succeeds_should_end_the_fallback()
    {
        // Arrange
        await _breaker.ReadTrippedTopicsAsync();
        var batch = _breaker.BeginTripBatch();
        batch.TripTopic(_orders);
        await batch.WriteAsync();
        _store.FailReads = false;

        // Act
        await _breaker.ReadTrippedTopicsAsync();

        // Assert
        Assert.False(_breaker.StoreStatus.IsUsingFallback);
        Assert.Null(_breaker.StoreStatus.LastFailure);
    }
}
