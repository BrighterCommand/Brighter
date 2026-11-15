#region License

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

# endregion

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed;

public class StaleReadTimeoutTests
{
    private static readonly TimeSpan s_storeTimeout = TimeSpan.FromSeconds(2);
    private readonly RoutingKey _orders = new("orders");

    [Fact]
    public async Task When_a_stale_read_times_out_should_keep_the_failure_that_holds_the_fallback()
    {
        // Arrange
        var clock = new FakeTimeProvider();
        var store = new IndividuallyHeldReadsTrippedTopicStore(new ThrowingOnWriteTrippedTopicStore());
        var breaker = new DistributedOutboxCircuitBreaker(store, new OutboxCircuitBreakerOptions { StoreTimeout = s_storeTimeout }, timeProvider: clock);
        breaker.StartRefresh();
        await store.ReadStarted(1);
        var newerRead = breaker.ReadTrippedTopicsAsync();
        await store.ReadStarted(2);
        store.CompleteRead(2, new HeldRead([], []));
        await newerRead;
        var batch = breaker.BeginTripBatch();
        batch.TripTopic(_orders);
        await batch.WriteAsync();

        // Act
        clock.Advance(s_storeTimeout);
        await Task.Delay(200);

        // Assert
        Assert.True(breaker.StoreStatus.IsUsingFallback);
        Assert.Equal("InvalidOperationException", breaker.StoreStatus.LastFailure);
    }
}
