using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.CircuitBreaker;
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker;

public class InMemoryConcurrentExpiryTests
{
    private const int REPETITIONS = 1000;

    [Test]
    public async Task When_two_cooldowns_run_concurrently_at_expiry_should_produce_one_reset()
    {
        using (TestCorrelator.CreateContext())
        {
            // Arrange
            var topics = Enumerable.Range(0, REPETITIONS).Select(i => new RoutingKey($"orders-{i}")).ToArray();
            var listedAfterExpiry = 0;

            // Act
            foreach (var topic in topics)
            {
                var clock = new FakeTimeProvider();
                var breaker = new InMemoryOutboxCircuitBreaker(
                    new OutboxCircuitBreakerOptions { CooldownCount = 10, MaximumTripDuration = TimeSpan.FromMinutes(5) },
                    timeProvider: clock);
                breaker.TripTopic(topic);
                clock.Advance(TimeSpan.FromMinutes(5));

                using var barrier = new Barrier(2);
                var racers = Enumerable.Range(0, 2).Select(_ => Task.Run(() =>
                {
                    barrier.SignalAndWait();
                    breaker.CoolDown();
                })).ToArray();
                Task.WaitAll(racers);

                if (breaker.TrippedTopics.Contains(topic))
                    listedAfterExpiry++;
            }

            // Assert
            var resetsPerTopic = TestCorrelator.GetLogEventsFromCurrentContext()
                .Where(e => e.Level == LogEventLevel.Information && e.MessageTemplate.Text.StartsWith("Circuit breaker reset for topic"))
                .GroupBy(e => e.Properties["Topic"].ToString())
                .ToDictionary(g => g.Key, g => g.Count());

            await Assert.That(resetsPerTopic.Count).IsEqualTo(REPETITIONS);
            foreach (var count in resetsPerTopic.Values)
{
    await Assert.That(count).IsEqualTo(1);
}
            await Assert.That(listedAfterExpiry).IsEqualTo(0);
        }
    }
}
