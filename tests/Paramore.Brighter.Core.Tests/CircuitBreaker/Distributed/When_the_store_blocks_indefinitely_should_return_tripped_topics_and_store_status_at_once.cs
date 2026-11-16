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

public class When_the_store_blocks_indefinitely_should_return_tripped_topics_and_store_status_at_once
{
    private static readonly TimeSpan s_bound = TimeSpan.FromSeconds(1);
    private readonly RoutingKey _orders = new("orders");

    private static async Task<bool> ReturnsWithinBoundAsync<T>(Func<T> call)
    {
        var running = Task.Run(call);
        return await Task.WhenAny(running, Task.Delay(s_bound)) == running;
    }

    [Fact]
    public async Task When_the_store_blocks_indefinitely_tripped_topics_and_store_status_should_return_within_a_second()
    {
        // Arrange
        var breaker = new DistributedOutboxCircuitBreaker(
            new NeverCompletingTrippedTopicStore(), new OutboxCircuitBreakerOptions(), timeProvider: new FakeTimeProvider());
        breaker.TripTopic(_orders);
        var readInFlight = breaker.ReadTrippedTopicsAsync();

        // Act
        var topicsReturned = await ReturnsWithinBoundAsync(() => breaker.TrippedTopics.ToArray());
        var statusReturned = await ReturnsWithinBoundAsync(() => breaker.StoreStatus);

        // Assert
        Assert.False(readInFlight.IsCompleted);
        Assert.True(topicsReturned);
        Assert.Equal([_orders], breaker.TrippedTopics.ToArray());
        Assert.True(statusReturned);
    }
}
