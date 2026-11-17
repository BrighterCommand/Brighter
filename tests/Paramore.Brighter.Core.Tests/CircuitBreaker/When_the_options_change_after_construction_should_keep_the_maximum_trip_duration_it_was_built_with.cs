using System;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.CircuitBreaker;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker;

public class InMemoryBreakerOptionsChangedAfterConstructionTests
{
    [Fact]
    public void When_the_options_change_after_construction_should_keep_the_maximum_trip_duration_it_was_built_with()
    {
        // Arrange
        var orders = new RoutingKey("orders");
        var clock = new FakeTimeProvider();
        var options = new OutboxCircuitBreakerOptions { CooldownCount = 10, MaximumTripDuration = TimeSpan.FromMinutes(5) };
        var breaker = new InMemoryOutboxCircuitBreaker(options, timeProvider: clock);

        // Act
        options.MaximumTripDuration = TimeSpan.FromMinutes(1);
        breaker.TripTopic(orders);

        // Assert
        clock.Advance(TimeSpan.FromMinutes(4) + TimeSpan.FromSeconds(59));
        Assert.Contains(orders, breaker.TrippedTopics);

        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.DoesNotContain(orders, breaker.TrippedTopics);
    }

    [Fact]
    public void When_the_cooldown_count_changes_after_construction_should_apply_it_to_the_next_trip()
    {
        // Arrange
        var payments = new RoutingKey("payments");
        var options = new OutboxCircuitBreakerOptions { CooldownCount = 10 };
        var breaker = new InMemoryOutboxCircuitBreaker(options);

        // Act
        options.CooldownCount = 0;
        breaker.TripTopic(payments);
        breaker.CoolDown();

        // Assert
        Assert.DoesNotContain(payments, breaker.TrippedTopics);
    }
}
