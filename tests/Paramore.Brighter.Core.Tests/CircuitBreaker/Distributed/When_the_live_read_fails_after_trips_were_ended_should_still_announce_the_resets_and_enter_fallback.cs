using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed.TestDoubles;
using Serilog.Sinks.TestCorrelator;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed;

public class DistributedBreakerLiveReadFailureTests
{
    private static readonly TimeSpan s_cooldown = TimeSpan.FromSeconds(50);
    private readonly RoutingKey _orders = new("orders");

    private static int Announced(string operation) => TestCorrelator.GetLogEventsFromCurrentContext()
        .Count(e => e.MessageTemplate.Text.StartsWith($"Circuit breaker {operation} for topic"));

    [Fact]
    public async Task When_the_live_read_fails_after_trips_were_ended_should_still_announce_the_resets_and_enter_fallback()
    {
        using (TestCorrelator.CreateContext())
        {
            // Arrange
            var clock = new FakeTimeProvider();
            var options = new OutboxCircuitBreakerOptions { CooldownDuration = s_cooldown, ClockSkewLeeway = TimeSpan.Zero };
            var breaker = new DistributedOutboxCircuitBreaker(
                new LiveReadFailingTrippedTopicStore(new InMemoryTrippedTopicStore()), options, timeProvider: clock);
            var batch = breaker.BeginTripBatch();
            batch.TripTopic(_orders);
            await batch.WriteAsync();
            clock.Advance(s_cooldown);

            // Act
            var topics = await breaker.ReadTrippedTopicsAsync();

            // Assert
            Assert.Equal(1, Announced("reset"));
            Assert.Empty(topics);
            Assert.True(breaker.StoreStatus.IsUsingFallback);
            Assert.Equal("InvalidOperationException", breaker.StoreStatus.LastFailure);
        }
    }
}
