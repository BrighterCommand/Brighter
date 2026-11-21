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
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed;

public class LocalTripKeepsLaterStoreStampTests
{
    private static readonly DateTimeOffset s_start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan s_cooldown = TimeSpan.FromSeconds(50);
    private static readonly TimeSpan s_aheadBy = TimeSpan.FromSeconds(5);
    private static readonly RoutingKey s_orders = new("orders");

    [Fact]
    public async Task When_a_local_trip_cannot_be_written_should_keep_the_later_stamp_read_from_the_store()
    {
        // Arrange
        var inner = new InMemoryTrippedTopicStore();
        var options = new OutboxCircuitBreakerOptions { CooldownDuration = s_cooldown, ClockSkewLeeway = s_aheadBy };
        var aheadClock = new FakeTimeProvider(s_start + s_aheadBy);
        var ahead = new DistributedOutboxCircuitBreaker(inner, options, timeProvider: aheadClock);
        var aheadBatch = ahead.BeginTripBatch();
        aheadBatch.TripTopic(s_orders);
        await aheadBatch.WriteAsync();

        var store = new WritesFailingOnDemandTrippedTopicStore(inner);
        var localClock = new FakeTimeProvider(s_start);
        var local = new DistributedOutboxCircuitBreaker(store, options, timeProvider: localClock);
        await local.ReadTrippedTopicsAsync();

        store.FailWrites = true;
        var localBatch = local.BeginTripBatch();
        localBatch.TripTopic(s_orders);
        await localBatch.WriteAsync();

        // Act
        localClock.Advance(s_cooldown + TimeSpan.FromSeconds(2));
        var listed = local.TrippedTopics.ToList();

        // Assert
        Assert.Contains(s_orders, listed);
    }
}
