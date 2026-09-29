using System.Linq;
using Paramore.Brighter.CircuitBreaker;
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;


namespace Paramore.Brighter.Core.Tests.CircuitBreaker
{
    public class OutboxCircuitBreakerReTripLoggingTests
    {
        [Test]
        public async System.Threading.Tasks.Task When_an_already_tripped_topic_fails_again_should_log_debug()
        {
            using (TestCorrelator.CreateContext())
            {
                // Arrange
                var topic = new RoutingKey("already.tripped.topic");
                var circuitBreaker = new InMemoryOutboxCircuitBreaker(new OutboxCircuitBreakerOptions { CooldownCount = 3 });
                circuitBreaker.TripTopic(topic); // fresh trip

                // Act
                circuitBreaker.TripTopic(topic); // re-trip

                // Assert
                var logEvents = TestCorrelator.GetLogEventsFromCurrentContext().ToList();

                // Only the fresh trip logs a Warning - the re-trip must not add a second one
                var warnings = logEvents.Where(e => e.Level == LogEventLevel.Warning).ToList();
                await Assert.That(warnings).HasSingleItem();

                await Assert.That(logEvents).Contains(e =>
                    e.Level == LogEventLevel.Debug &&
                    e.MessageTemplate.Text == "Circuit breaker re-tripped for topic {Topic}; cooldown extended to {CooldownCount} cycle(s)" &&
                    e.Properties["Topic"].ToString() == "\"already.tripped.topic\"" &&
                    e.Properties["CooldownCount"].ToString() == "3");
            }
        }
    }
}
