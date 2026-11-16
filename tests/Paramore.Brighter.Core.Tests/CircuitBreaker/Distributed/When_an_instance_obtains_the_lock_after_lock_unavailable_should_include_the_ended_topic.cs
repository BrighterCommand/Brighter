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

public class InstanceAfterLockUnavailableTests
{
    private static readonly DateTimeOffset s_start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly RoutingKey s_orders = new("orders");

    [Fact]
    public async Task When_an_instance_obtains_the_lock_after_lock_unavailable_should_include_the_ended_topic()
    {
        // Arrange
        var store = new InMemoryTrippedTopicStore();
        var clockA = new FakeTimeProvider(s_start);
        var clockB = new FakeTimeProvider(s_start);
        var breakerA = new DistributedOutboxCircuitBreaker(store, timeProvider: clockA);
        var breakerB = new DistributedOutboxCircuitBreaker(store, timeProvider: clockB);
        var batch = breakerA.BeginTripBatch();
        batch.TripTopic(s_orders);
        await batch.WriteAsync();

        clockA.Advance(TimeSpan.FromSeconds(50));
        clockB.Advance(TimeSpan.FromSeconds(50));
        await breakerB.ReadTrippedTopicsAsync();
        clockA.Advance(TimeSpan.FromSeconds(50));
        clockB.Advance(TimeSpan.FromSeconds(50));

        // Act
        var listedBeforeRead = breakerA.TrippedTopics.ToList();
        var tripped = await breakerA.ReadTrippedTopicsAsync();

        // Assert
        Assert.DoesNotContain(s_orders, listedBeforeRead);
        Assert.DoesNotContain(s_orders, tripped);
    }
}
