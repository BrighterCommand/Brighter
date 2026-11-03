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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed;

public class When_the_store_never_completes_should_give_up_after_the_store_timeout
{
    private static readonly TimeSpan s_storeTimeout = TimeSpan.FromSeconds(2);
    private readonly RoutingKey _orders = new("orders");
    private readonly RoutingKey _payments = new("payments");

    private static DistributedOutboxCircuitBreaker CreateBreaker(FakeTimeProvider clock)
        => new(new NeverCompletingTrippedTopicStore(), new OutboxCircuitBreakerOptions { StoreTimeout = s_storeTimeout }, timeProvider: clock);

    private static async Task<bool> CompletesAsync(Task task)
        => await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(10))) == task;

    [Fact]
    public async Task When_the_store_never_completes_should_give_up_after_the_store_timeout_and_use_the_fallback_view()
    {
        // Arrange
        var clock = new FakeTimeProvider();
        var breaker = CreateBreaker(clock);
        breaker.TripTopic(_orders);

        // Act
        var read = breaker.ReadTrippedTopicsAsync();
        clock.Advance(s_storeTimeout);
        var returned = await CompletesAsync(read);

        // Assert
        Assert.True(returned);
        Assert.Equal([_orders], (await read).ToArray());
        Assert.True(breaker.StoreStatus.IsUsingFallback);
        Assert.Equal("timeout", breaker.StoreStatus.LastFailure);
    }

    [Fact]
    public async Task When_the_store_never_completes_a_batch_write_should_give_up_after_the_store_timeout_and_leave_the_topics_unshared()
    {
        // Arrange
        var clock = new FakeTimeProvider();
        var breaker = CreateBreaker(clock);
        var batch = breaker.BeginTripBatch();
        batch.TripTopic(_orders);
        batch.TripTopic(_payments);

        // Act
        var write = batch.WriteAsync();
        clock.Advance(s_storeTimeout);
        var returned = await CompletesAsync(write);

        // Assert
        Assert.True(returned);
        Assert.True(breaker.StoreStatus.IsUsingFallback);
        Assert.Equal("timeout", breaker.StoreStatus.LastFailure);
        Assert.Equal(2, breaker.TrippedTopics.Count());
    }

    [Fact]
    public async Task When_the_caller_cancels_a_read_should_throw_and_change_no_state()
    {
        // Arrange
        var breaker = CreateBreaker(new FakeTimeProvider());
        using var cancellation = new CancellationTokenSource();

        // Act
        var read = breaker.ReadTrippedTopicsAsync(cancellation.Token);
        cancellation.Cancel();
        var returned = await CompletesAsync(read);

        // Assert
        Assert.True(returned);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => read);
        Assert.False(breaker.StoreStatus.IsUsingFallback);
        Assert.Null(breaker.StoreStatus.LastFailure);
    }
}
