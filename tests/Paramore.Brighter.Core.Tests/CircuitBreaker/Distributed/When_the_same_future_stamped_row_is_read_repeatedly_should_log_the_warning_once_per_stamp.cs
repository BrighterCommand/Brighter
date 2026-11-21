#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
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
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed;

public class RepeatedFutureStampedReadLoggingTests
{
    private static readonly DateTimeOffset s_start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan s_leeway = TimeSpan.FromSeconds(5);
    private readonly RoutingKey _orders = new("orders");

    [Fact]
    public async Task When_the_same_future_stamped_row_is_read_repeatedly_should_log_the_warning_once_per_stamp()
    {
        // Arrange
        using var context = TestCorrelator.CreateContext();
        var store = new InMemoryTrippedTopicStore();
        var breaker = new DistributedOutboxCircuitBreaker(
            store, new OutboxCircuitBreakerOptions { ClockSkewLeeway = s_leeway }, timeProvider: new FakeTimeProvider(s_start));
        await store.WriteAsync([new TopicTrip(_orders, s_start + TimeSpan.FromHours(1))]);

        // Act
        await breaker.ReadTrippedTopicsAsync();
        await breaker.ReadTrippedTopicsAsync();
        await breaker.ReadTrippedTopicsAsync();
        var afterSameStamp = Warnings();
        await store.WriteAsync([new TopicTrip(_orders, s_start + TimeSpan.FromHours(2))]);
        await breaker.ReadTrippedTopicsAsync();
        await breaker.ReadTrippedTopicsAsync();

        // Assert
        Assert.Equal(1, afterSameStamp);
        Assert.Equal(2, Warnings());
    }

    private static int Warnings() => TestCorrelator.GetLogEventsFromCurrentContext()
        .Count(e => e.Level == LogEventLevel.Warning && e.RenderMessage().Contains("orders"));
}
