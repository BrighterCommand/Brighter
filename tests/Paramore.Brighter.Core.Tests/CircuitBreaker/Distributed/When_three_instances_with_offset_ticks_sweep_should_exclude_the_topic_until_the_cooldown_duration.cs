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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.CircuitBreaker;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed;

public class OffsetTicksCooldownTests
{
    private static readonly DateTimeOffset s_start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan s_interval = TimeSpan.FromSeconds(5);
    private static readonly RoutingKey s_orders = new("orders");

    [Fact]
    public async Task When_three_instances_with_offset_ticks_sweep_should_exclude_the_topic_until_the_cooldown_duration()
    {
        // Arrange
        var store = new InMemoryTrippedTopicStore();
        var offsets = new[] { TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(1800), TimeSpan.FromMilliseconds(3500) };
        var clocks = offsets.Select(_ => new FakeTimeProvider(s_start)).ToArray();
        var breakers = clocks.Select(clock => new DistributedOutboxCircuitBreaker(store, timeProvider: clock)).ToArray();
        var batch = breakers[0].BeginTripBatch();
        batch.TripTopic(s_orders);
        await batch.WriteAsync();

        var ticks = Enumerable.Range(0, 4)
            .SelectMany(round => offsets.Select((offset, instance) => (Instance: instance, At: offset + round * s_interval)))
            .OrderBy(tick => tick.At)
            .ToList();

        // Act
        var excludedAtEachRead = new List<bool>();
        foreach (var tick in ticks)
        {
            foreach (var clock in clocks)
                clock.SetUtcNow(s_start + tick.At);
            var tripped = await breakers[tick.Instance].ReadTrippedTopicsAsync();
            excludedAtEachRead.Add(tripped.Contains(s_orders));
        }

        foreach (var clock in clocks)
            clock.SetUtcNow(s_start + TimeSpan.FromSeconds(50.1));
        var afterCooldown = await breakers[0].ReadTrippedTopicsAsync();

        // Assert
        Assert.Equal(12, excludedAtEachRead.Count);
        Assert.All(excludedAtEachRead, Assert.True);
        Assert.DoesNotContain(s_orders, afterCooldown);
    }
}
