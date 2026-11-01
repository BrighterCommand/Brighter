using System;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.CircuitBreaker;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker;

public class InMemoryReTripRestartsBothLimitsTests
{
    [Fact]
    public void When_a_tripped_topic_is_re_tripped_should_restart_the_maximum_trip_duration()
    {
        // Arrange
        var orders = new RoutingKey("orders");
        var clock = new FakeTimeProvider();
        var breaker = new InMemoryOutboxCircuitBreaker(
            new OutboxCircuitBreakerOptions { CooldownCount = 10, MaximumTripDuration = TimeSpan.FromMinutes(5) },
            timeProvider: clock);
        breaker.TripTopic(orders);
        clock.Advance(TimeSpan.FromMinutes(4));

        // Act
        breaker.TripTopic(orders);

        // Assert
        clock.Advance(TimeSpan.FromMinutes(4) + TimeSpan.FromSeconds(59));
        Assert.Contains(orders, breaker.TrippedTopics);

        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.DoesNotContain(orders, breaker.TrippedTopics);
    }

    [Fact]
    public void When_a_tripped_topic_is_re_tripped_should_restart_the_cooldown_count()
    {
        // Arrange
        var orders = new RoutingKey("orders");
        var breaker = new InMemoryOutboxCircuitBreaker(
            new OutboxCircuitBreakerOptions { CooldownCount = 2, MaximumTripDuration = TimeSpan.FromMinutes(5) },
            timeProvider: new FakeTimeProvider());
        breaker.TripTopic(orders);
        breaker.CoolDown();
        breaker.CoolDown();

        // Act
        breaker.TripTopic(orders);
        breaker.CoolDown();
        breaker.CoolDown();

        // Assert
        Assert.Contains(orders, breaker.TrippedTopics);

        breaker.CoolDown();
        Assert.DoesNotContain(orders, breaker.TrippedTopics);
    }
}
