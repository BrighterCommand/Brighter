using System;
using System.Linq;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.CircuitBreaker;
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker;

public class InMemoryBothLimitsPassTests
{
    [Fact]
    public void When_both_limits_pass_in_one_cooldown_should_name_the_maximum_trip_duration()
    {
        using (TestCorrelator.CreateContext())
        {
            // Arrange
            var orders = new RoutingKey("orders");
            var clock = new FakeTimeProvider();
            var breaker = new InMemoryOutboxCircuitBreaker(
                new OutboxCircuitBreakerOptions { CooldownCount = 1, MaximumTripDuration = TimeSpan.FromMinutes(5) },
                timeProvider: clock);
            breaker.TripTopic(orders);
            clock.Advance(TimeSpan.FromMinutes(1));
            breaker.CoolDown();

            // Act
            clock.Advance(TimeSpan.FromMinutes(4));
            breaker.CoolDown();

            // Assert
            var resets = TestCorrelator.GetLogEventsFromCurrentContext()
                .Where(e => e.Level == LogEventLevel.Information && e.MessageTemplate.Text.StartsWith("Circuit breaker reset for topic"))
                .ToList();
            var reset = Assert.Single(resets);
            Assert.Equal("\"maximum trip duration\"", reset.Properties["ResetCause"].ToString());
        }
    }
}
