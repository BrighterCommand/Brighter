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
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed;

public class StoreReadTimeoutTests
{
    private static readonly TimeSpan s_storeTimeout = TimeSpan.FromSeconds(2);
    private readonly RoutingKey _orders = new("orders");

    [Fact]
    public async Task When_a_store_read_times_out_should_log_a_warning_and_use_the_fallback_view()
    {
        // Arrange
        using var context = TestCorrelator.CreateContext();
        var clock = new FakeTimeProvider();
        var breaker = new DistributedOutboxCircuitBreaker(
            new NeverCompletingTrippedTopicStore(),
            new OutboxCircuitBreakerOptions { StoreTimeout = s_storeTimeout },
            timeProvider: clock);
        breaker.TripTopic(_orders);

        // Act
        var read = breaker.ReadTrippedTopicsAsync();
        clock.Advance(s_storeTimeout);
        var topics = await read;

        // Assert
        Assert.Equal([_orders], topics.ToArray());
        Assert.True(breaker.StoreStatus.IsUsingFallback);
        Assert.Equal("timeout", breaker.StoreStatus.LastFailure);
        Assert.Contains(TestCorrelator.GetLogEventsFromCurrentContext(),
            e => e.Level == LogEventLevel.Warning && e.RenderMessage().Contains("timeout"));
    }
}
