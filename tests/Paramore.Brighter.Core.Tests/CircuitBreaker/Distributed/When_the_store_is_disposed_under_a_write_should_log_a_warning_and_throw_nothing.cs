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

public class When_the_store_is_disposed_under_a_write_should_log_a_warning_and_throw_nothing
{
    private static readonly DateTimeOffset s_start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan s_cooldown = TimeSpan.FromSeconds(50);
    private static readonly OutboxCircuitBreakerOptions s_options = new() { CooldownDuration = s_cooldown, ClockSkewLeeway = TimeSpan.Zero };
    private static readonly TimeSpan s_patience = TimeSpan.FromSeconds(10);
    private readonly RoutingKey _orders = new("orders");

    private static bool NamesTheDisposal(LogEvent e)
        => e.Level == LogEventLevel.Warning && e.RenderMessage().Contains("ObjectDisposedException");

    private static async Task<bool> BecomesTrueAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + s_patience;
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(5);

        return condition();
    }

    [Fact]
    public async Task When_a_batch_write_is_disposed_under_should_log_a_warning_throw_nothing_and_keep_the_trip_unshared()
    {
        using var context = TestCorrelator.CreateContext();
        // Arrange
        var clock = new FakeTimeProvider(s_start);
        var breaker = new DistributedOutboxCircuitBreaker(new DisposedOnWriteTrippedTopicStore(), s_options, timeProvider: clock);
        var batch = breaker.BeginTripBatch();
        batch.TripTopic(_orders);

        // Act
        var exception = await Record.ExceptionAsync(() => batch.WriteAsync());
        clock.Advance(TimeSpan.FromSeconds(20));
        var listedUntilCooldown = await breaker.ReadTrippedTopicsAsync();
        clock.Advance(s_cooldown);
        var listedAfterCooldown = await breaker.ReadTrippedTopicsAsync();

        // Assert
        Assert.Null(exception);
        Assert.Contains(TestCorrelator.GetLogEventsFromCurrentContext(), NamesTheDisposal);
        Assert.Equal([_orders], listedUntilCooldown.ToArray());
        Assert.Empty(listedAfterCooldown);
    }

    [Fact]
    public async Task When_a_callback_write_is_disposed_under_should_log_a_warning_throw_nothing_and_keep_the_trip_unshared()
    {
        using var context = TestCorrelator.CreateContext();
        // Arrange
        var store = new DisposedOnWriteTrippedTopicStore();
        var clock = new FakeTimeProvider(s_start);
        var breaker = new DistributedOutboxCircuitBreaker(store, s_options, timeProvider: clock);

        // Act
        var exception = Record.Exception(() => breaker.TripTopic(_orders));
        var attempted = await BecomesTrueAsync(() => store.WriteCount == 1 && breaker.StoreStatus.IsUsingFallback && TestCorrelator.GetLogEventsFromCurrentContext().Any(NamesTheDisposal));
        clock.Advance(TimeSpan.FromSeconds(20));
        var listedUntilCooldown = await breaker.ReadTrippedTopicsAsync();
        clock.Advance(s_cooldown);
        var listedAfterCooldown = await breaker.ReadTrippedTopicsAsync();

        // Assert
        Assert.Null(exception);
        Assert.True(attempted);
        Assert.Contains(TestCorrelator.GetLogEventsFromCurrentContext(), NamesTheDisposal);
        Assert.Equal([_orders], listedUntilCooldown.ToArray());
        Assert.Empty(listedAfterCooldown);
    }
}
