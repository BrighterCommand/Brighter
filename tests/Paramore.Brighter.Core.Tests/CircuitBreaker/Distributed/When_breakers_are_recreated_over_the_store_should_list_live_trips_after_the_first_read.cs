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
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed;

public class RecreatedBreakersTests
{
    private static readonly DateTimeOffset s_start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly RoutingKey s_orders = new("orders");

    [Fact]
    public async Task When_every_breaker_is_recreated_over_a_durable_store_should_list_a_live_trip_after_the_first_read_and_drop_an_ended_one()
    {
        // Arrange
        var store = await StoreWithOrdersTrippedAtStart();
        var recreated = new DistributedOutboxCircuitBreaker(store, timeProvider: new FakeTimeProvider(s_start.AddSeconds(10)));

        // Act
        var listedBeforeRead = recreated.TrippedTopics.ToList();
        var tripped = await recreated.ReadTrippedTopicsAsync();

        // Assert
        Assert.DoesNotContain(s_orders, listedBeforeRead);
        Assert.Contains(s_orders, tripped);
        Assert.Contains(s_orders, recreated.TrippedTopics);
    }

    [Fact]
    public async Task When_breakers_are_recreated_after_the_cooldown_should_not_list_the_trip_after_a_read()
    {
        // Arrange
        var store = await StoreWithOrdersTrippedAtStart();
        var recreated = new DistributedOutboxCircuitBreaker(store, timeProvider: new FakeTimeProvider(s_start.AddSeconds(60)));

        // Act
        var tripped = await recreated.ReadTrippedTopicsAsync();

        // Assert
        Assert.DoesNotContain(s_orders, tripped);
        Assert.DoesNotContain(s_orders, recreated.TrippedTopics);
    }

    [Fact]
    public async Task When_no_sweeper_runs_should_still_end_the_trip_at_the_cooldown_duration()
    {
        // Arrange
        var store = await StoreWithOrdersTrippedAtStart();
        var clock = new FakeTimeProvider(s_start.AddSeconds(10));
        var recreated = new DistributedOutboxCircuitBreaker(store, timeProvider: clock);
        var listedAtTenSeconds = await recreated.ReadTrippedTopicsAsync();

        // Act
        clock.Advance(TimeSpan.FromSeconds(40));
        var listedAtCooldown = await recreated.ReadTrippedTopicsAsync();

        // Assert
        Assert.Contains(s_orders, listedAtTenSeconds);
        Assert.DoesNotContain(s_orders, listedAtCooldown);
    }

    private static async Task<InMemoryTrippedTopicStore> StoreWithOrdersTrippedAtStart()
    {
        var store = new InMemoryTrippedTopicStore();
        var breaker = new DistributedOutboxCircuitBreaker(store, timeProvider: new FakeTimeProvider(s_start));
        var batch = breaker.BeginTripBatch();
        batch.TripTopic(s_orders);
        await batch.WriteAsync();
        return store;
    }
}
