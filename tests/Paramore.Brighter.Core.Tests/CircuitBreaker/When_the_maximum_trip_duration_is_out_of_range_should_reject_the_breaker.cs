using System;
using Paramore.Brighter.CircuitBreaker;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker;

public class MaximumTripDurationValidationTests
{
    public static TheoryData<TimeSpan> OutOfRangeDurations => new()
    {
        TimeSpan.Zero,
        TimeSpan.FromSeconds(-1),
        System.Threading.Timeout.InfiniteTimeSpan,
        TimeSpan.MaxValue,
        TimeSpan.FromHours(24) + TimeSpan.FromMilliseconds(1)
    };

    public static TheoryData<TimeSpan> InRangeDurations => new()
    {
        TimeSpan.FromMilliseconds(1),
        TimeSpan.FromHours(24)
    };

    [Theory]
    [MemberData(nameof(OutOfRangeDurations))]
    public void When_the_maximum_trip_duration_is_out_of_range_should_reject_the_breaker(TimeSpan maximumTripDuration)
    {
        // Arrange
        var options = new OutboxCircuitBreakerOptions { MaximumTripDuration = maximumTripDuration };

        // Act
        var construct = () => new InMemoryOutboxCircuitBreaker(options);

        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(construct);
    }

    [Theory]
    [MemberData(nameof(InRangeDurations))]
    public void When_the_maximum_trip_duration_is_in_range_should_accept_the_breaker(TimeSpan maximumTripDuration)
    {
        // Arrange
        var options = new OutboxCircuitBreakerOptions { MaximumTripDuration = maximumTripDuration };

        // Act
        var breaker = new InMemoryOutboxCircuitBreaker(options);

        // Assert
        Assert.NotNull(breaker);
    }

    [Fact]
    public void When_options_are_default_should_set_a_five_minute_maximum_trip_duration_and_cooldown_count_of_ten()
    {
        // Arrange & Act
        var options = new OutboxCircuitBreakerOptions();

        // Assert
        Assert.Equal(TimeSpan.FromMinutes(5), options.MaximumTripDuration);
        Assert.Equal(10, options.CooldownCount);
    }
}
