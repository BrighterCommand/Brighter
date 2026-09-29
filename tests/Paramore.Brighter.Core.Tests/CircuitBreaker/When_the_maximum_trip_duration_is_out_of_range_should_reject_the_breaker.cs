using System;
using Paramore.Brighter.CircuitBreaker;
using System.Threading.Tasks;
namespace Paramore.Brighter.Core.Tests.CircuitBreaker;

public class MaximumTripDurationValidationTests
{
    public static List<TimeSpan> OutOfRangeDurations => new()
    {
        TimeSpan.Zero,        TimeSpan.FromSeconds(-1),        System.Threading.Timeout.InfiniteTimeSpan,        TimeSpan.MaxValue,        TimeSpan.FromHours(24) + TimeSpan.FromMilliseconds(1)
    };

    public static List<TimeSpan> InRangeDurations => new()
    {
        TimeSpan.FromMilliseconds(1),        TimeSpan.FromHours(24)
    };

    [Test]
    [MethodDataSource(nameof(OutOfRangeDurations))]
    public async System.Threading.Tasks.Task When_the_maximum_trip_duration_is_out_of_range_should_reject_the_breaker(TimeSpan maximumTripDuration)
    {
        // Arrange
        var options = new OutboxCircuitBreakerOptions { MaximumTripDuration = maximumTripDuration };

        // Act
        var construct = () => new InMemoryOutboxCircuitBreaker(options);

        // Assert
        await Assert.That(construct).ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    [MethodDataSource(nameof(InRangeDurations))]
    public async Task When_the_maximum_trip_duration_is_in_range_should_accept_the_breaker(TimeSpan maximumTripDuration)
    {
        // Arrange
        var options = new OutboxCircuitBreakerOptions { MaximumTripDuration = maximumTripDuration };

        // Act
        var breaker = new InMemoryOutboxCircuitBreaker(options);

        // Assert
        await Assert.That(breaker).IsNotNull();
    }

    [Test]
    public async Task When_options_are_default_should_set_a_five_minute_maximum_trip_duration_and_cooldown_count_of_ten()
    {
        // Arrange & Act
        var options = new OutboxCircuitBreakerOptions();

        // Assert
        await Assert.That(options.MaximumTripDuration).IsEqualTo(TimeSpan.FromMinutes(5));
        await Assert.That(options.CooldownCount).IsEqualTo(10);
    }
}
