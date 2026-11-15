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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed;

public class DistributedBreakerRetripAtWriteEndTests
{
    private const int Topics = 20_000;

    [Fact]
    public async Task When_a_topic_is_tripped_again_as_its_write_finishes_should_still_write_the_retrip()
    {
        // Arrange
        var store = new CountingTrippedTopicStore();
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var breaker = new DistributedOutboxCircuitBreaker(store, new OutboxCircuitBreakerOptions(), timeProvider: clock);
        var retrips = new List<TopicTrip>(Topics);

        // Act
        for (var i = 0; i < Topics; i++)
        {
            var topic = new RoutingKey($"orders-{i}");
            breaker.TripTopic(topic);
            Thread.SpinWait(i % 200);
            clock.Advance(TimeSpan.FromMilliseconds(1));
            retrips.Add(new TopicTrip(topic, clock.GetUtcNow()));
            breaker.TripTopic(topic);
        }

        var unwritten = await UnwrittenAsync(store, retrips);

        // Assert
        Assert.Empty(unwritten);
    }

    private static async Task<List<TopicTrip>> UnwrittenAsync(CountingTrippedTopicStore store, List<TopicTrip> retrips)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        List<TopicTrip> unwritten;
        do
        {
            var written = store.Writes.SelectMany(write => write).ToHashSet();
            unwritten = retrips.Where(retrip => !written.Contains(retrip)).ToList();
            if (unwritten.Count > 0)
                await Task.Delay(100);
        } while (unwritten.Count > 0 && DateTime.UtcNow < deadline);

        return unwritten;
    }
}
