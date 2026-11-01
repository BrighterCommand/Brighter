using System;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.CircuitBreaker;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker;

public class InMemoryCooldownCountShorterThanMaximumTripDurationTests
{
    [Fact]
    public void When_the_cooldown_count_runs_out_before_the_maximum_trip_duration_should_end_the_trip()
    {
        // Arrange
        var orders = new RoutingKey("orders");
        var clock = new FakeTimeProvider();
        var breaker = new InMemoryOutboxCircuitBreaker(
            new OutboxCircuitBreakerOptions { CooldownCount = 2, MaximumTripDuration = TimeSpan.FromMinutes(5) },
            timeProvider: clock);
        breaker.TripTopic(orders);

        // Act
        breaker.CoolDown();
        breaker.CoolDown();

        // Assert
        Assert.Contains(orders, breaker.TrippedTopics);

        breaker.CoolDown();
        Assert.DoesNotContain(orders, breaker.TrippedTopics);
    }
}
