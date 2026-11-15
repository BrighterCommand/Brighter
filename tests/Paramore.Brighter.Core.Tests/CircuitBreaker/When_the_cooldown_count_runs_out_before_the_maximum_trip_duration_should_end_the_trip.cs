using System;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.CircuitBreaker;
using System.Threading.Tasks;
namespace Paramore.Brighter.Core.Tests.CircuitBreaker;

public class InMemoryCooldownCountShorterThanMaximumTripDurationTests
{
    [Test]
    public async Task When_the_cooldown_count_runs_out_before_the_maximum_trip_duration_should_end_the_trip()
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
        await Assert.That(breaker.TrippedTopics).Contains(orders);

        breaker.CoolDown();
        await Assert.That(breaker.TrippedTopics).DoesNotContain(orders);
    }
}
