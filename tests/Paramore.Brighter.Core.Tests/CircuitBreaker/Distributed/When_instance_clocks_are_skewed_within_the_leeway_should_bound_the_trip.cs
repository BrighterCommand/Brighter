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

public class ClockSkewLeewayTests
{
    private static readonly DateTimeOffset s_start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan s_cooldown = TimeSpan.FromSeconds(50);
    private static readonly TimeSpan s_skew = TimeSpan.FromSeconds(5);
    private static readonly RoutingKey s_orders = new("orders");

    [Theory]
    [InlineData(5000)]
    [InlineData(2500)]
    public async Task When_instance_clocks_differ_by_a_skew_covered_by_the_leeway_should_list_the_topic_until_the_cooldown_and_drop_it_by_the_cooldown_plus_twice_the_skew(int actualDifferenceMilliseconds)
    {
        // Arrange
        var store = new InMemoryTrippedTopicStore();
        var options = new OutboxCircuitBreakerOptions { CooldownDuration = s_cooldown, ClockSkewLeeway = s_skew };
        var tripperClock = new FakeTimeProvider(s_start);
        var aheadClock = new FakeTimeProvider(s_start.AddMilliseconds(actualDifferenceMilliseconds));
        var tripper = new DistributedOutboxCircuitBreaker(store, options, timeProvider: tripperClock);
        var ahead = new DistributedOutboxCircuitBreaker(store, options, timeProvider: aheadClock);

        var batch = tripper.BeginTripBatch();
        batch.TripTopic(s_orders);
        await batch.WriteAsync();

        var oneMillisecondBeforeCooldown = s_cooldown - TimeSpan.FromMilliseconds(1);
        tripperClock.Advance(oneMillisecondBeforeCooldown);
        aheadClock.Advance(oneMillisecondBeforeCooldown);

        // Act
        var aheadListsBeforeCooldown = await ahead.ReadTrippedTopicsAsync();
        var aheadViewBeforeCooldown = ahead.TrippedTopics.ToList();
        var tripperListsBeforeCooldown = await tripper.ReadTrippedTopicsAsync();
        var tripperViewBeforeCooldown = tripper.TrippedTopics.ToList();

        var afterCooldownPlusTwiceSkew = s_cooldown + s_skew + s_skew + TimeSpan.FromMilliseconds(1) - oneMillisecondBeforeCooldown;
        tripperClock.Advance(afterCooldownPlusTwiceSkew);
        aheadClock.Advance(afterCooldownPlusTwiceSkew);

        var aheadListsAfterSkew = await ahead.ReadTrippedTopicsAsync();
        var tripperListsAfterSkew = await tripper.ReadTrippedTopicsAsync();

        // Assert
        Assert.Contains(s_orders, aheadListsBeforeCooldown);
        Assert.Contains(s_orders, aheadViewBeforeCooldown);
        Assert.Contains(s_orders, tripperListsBeforeCooldown);
        Assert.Contains(s_orders, tripperViewBeforeCooldown);
        Assert.DoesNotContain(s_orders, aheadListsAfterSkew);
        Assert.DoesNotContain(s_orders, ahead.TrippedTopics);
        Assert.DoesNotContain(s_orders, tripperListsAfterSkew);
        Assert.DoesNotContain(s_orders, tripper.TrippedTopics);
    }
}
