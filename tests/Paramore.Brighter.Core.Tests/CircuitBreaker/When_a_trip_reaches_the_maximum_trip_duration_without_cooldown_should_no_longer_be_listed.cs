using System;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.CircuitBreaker;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker;

public class InMemoryTripExpiryWithoutCooldownTests
{
    [Fact]
    public void When_a_trip_reaches_the_maximum_trip_duration_without_cooldown_should_no_longer_be_listed()
    {
        // Arrange
        var orders = new RoutingKey("orders");
        var clock = new FakeTimeProvider();
        var breaker = new InMemoryOutboxCircuitBreaker(
            new OutboxCircuitBreakerOptions { CooldownCount = 10, MaximumTripDuration = TimeSpan.FromMinutes(5) },
            timeProvider: clock);
        breaker.TripTopic(orders);

        // Act & Assert
        clock.Advance(TimeSpan.FromMinutes(5) - TimeSpan.FromMilliseconds(1));
        Assert.Contains(orders, breaker.TrippedTopics);

        clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.DoesNotContain(orders, breaker.TrippedTopics);

        clock.Advance(TimeSpan.FromMinutes(5));
        Assert.Empty(breaker.TrippedTopics);
    }
}
