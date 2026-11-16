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
using Paramore.Brighter.CircuitBreaker;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed;

public class DistributedBreakerDurationValidationTests
{
    public static TheoryData<TimeSpan> OutOfRangeCooldownDurations => new()
    {
        TimeSpan.Zero,
        TimeSpan.FromSeconds(-1),
        System.Threading.Timeout.InfiniteTimeSpan,
        TimeSpan.MaxValue,
        TimeSpan.FromHours(24) + TimeSpan.FromMilliseconds(1)
    };

    public static TheoryData<TimeSpan> InRangeCooldownDurations => new()
    {
        TimeSpan.FromMilliseconds(1),
        TimeSpan.FromHours(24)
    };

    public static TheoryData<TimeSpan> OutOfRangeLeeways => new()
    {
        TimeSpan.FromMilliseconds(-1),
        System.Threading.Timeout.InfiniteTimeSpan,
        TimeSpan.MinValue,
        TimeSpan.MaxValue,
        TimeSpan.FromHours(24) + TimeSpan.FromMilliseconds(1)
    };

    public static TheoryData<TimeSpan> InRangeLeeways => new()
    {
        TimeSpan.Zero,
        TimeSpan.FromHours(24)
    };

    public static TheoryData<TimeSpan> OutOfRangeStoreTimeouts => new()
    {
        TimeSpan.Zero,
        TimeSpan.FromSeconds(-1),
        System.Threading.Timeout.InfiniteTimeSpan,
        TimeSpan.MaxValue,
        TimeSpan.FromMilliseconds(999),
        TimeSpan.FromSeconds(60) + TimeSpan.FromMilliseconds(1)
    };

    public static TheoryData<TimeSpan> InRangeStoreTimeouts => new()
    {
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(60)
    };

    [Theory]
    [MemberData(nameof(OutOfRangeCooldownDurations))]
    public void When_the_distributed_breaker_is_built_with_an_out_of_range_duration_should_throw(TimeSpan cooldownDuration)
    {
        // Arrange
        var options = new OutboxCircuitBreakerOptions { CooldownDuration = cooldownDuration };

        // Act
        var construct = () => new DistributedOutboxCircuitBreaker(new InMemoryTrippedTopicStore(), options);

        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(construct);
    }

    [Theory]
    [MemberData(nameof(InRangeCooldownDurations))]
    public void When_the_distributed_breaker_is_built_with_an_in_range_cooldown_duration_should_accept(TimeSpan cooldownDuration)
    {
        // Arrange
        var options = new OutboxCircuitBreakerOptions { CooldownDuration = cooldownDuration };

        // Act
        var breaker = new DistributedOutboxCircuitBreaker(new InMemoryTrippedTopicStore(), options);

        // Assert
        Assert.NotNull(breaker);
    }

    [Theory]
    [MemberData(nameof(OutOfRangeLeeways))]
    public void When_the_distributed_breaker_is_built_with_an_out_of_range_leeway_should_throw(TimeSpan leeway)
    {
        // Arrange
        var options = new OutboxCircuitBreakerOptions { ClockSkewLeeway = leeway };

        // Act
        var construct = () => new DistributedOutboxCircuitBreaker(new InMemoryTrippedTopicStore(), options);

        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(construct);
    }

    [Theory]
    [MemberData(nameof(InRangeLeeways))]
    public void When_the_distributed_breaker_is_built_with_an_in_range_leeway_should_accept(TimeSpan leeway)
    {
        // Arrange
        var options = new OutboxCircuitBreakerOptions { ClockSkewLeeway = leeway };

        // Act
        var breaker = new DistributedOutboxCircuitBreaker(new InMemoryTrippedTopicStore(), options);

        // Assert
        Assert.NotNull(breaker);
    }

    [Theory]
    [MemberData(nameof(OutOfRangeStoreTimeouts))]
    public void When_the_distributed_breaker_is_built_with_an_out_of_range_store_timeout_should_throw(TimeSpan storeTimeout)
    {
        // Arrange
        var options = new OutboxCircuitBreakerOptions { StoreTimeout = storeTimeout };

        // Act
        var construct = () => new DistributedOutboxCircuitBreaker(new InMemoryTrippedTopicStore(), options);

        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(construct);
    }

    [Theory]
    [MemberData(nameof(InRangeStoreTimeouts))]
    public void When_the_distributed_breaker_is_built_with_an_in_range_store_timeout_should_accept(TimeSpan storeTimeout)
    {
        // Arrange
        var options = new OutboxCircuitBreakerOptions { StoreTimeout = storeTimeout };

        // Act
        var breaker = new DistributedOutboxCircuitBreaker(new InMemoryTrippedTopicStore(), options);

        // Assert
        Assert.NotNull(breaker);
    }

    [Fact]
    public void When_the_distributed_breaker_is_built_with_a_null_store_should_throw()
    {
        // Arrange & Act
        var construct = () => new DistributedOutboxCircuitBreaker(null!);

        // Assert
        Assert.Throws<ArgumentNullException>(construct);
    }
}
