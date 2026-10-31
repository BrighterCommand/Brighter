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
