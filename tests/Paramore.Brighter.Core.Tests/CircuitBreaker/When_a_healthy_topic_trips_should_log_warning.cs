using Paramore.Brighter.CircuitBreaker;
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;


namespace Paramore.Brighter.Core.Tests.CircuitBreaker
{
    public class OutboxCircuitBreakerTripLoggingTests
    {
        [Test]
        public async System.Threading.Tasks.Task When_a_healthy_topic_trips_should_log_warning()
        {
            using (TestCorrelator.CreateContext())
            {
                // Arrange
                var topic = new RoutingKey("healthy.topic");
                var circuitBreaker = new InMemoryOutboxCircuitBreaker(new OutboxCircuitBreakerOptions { CooldownCount = 3 });

                // Act
                circuitBreaker.TripTopic(topic);

                // Assert
                var logEvents = TestCorrelator.GetLogEventsFromCurrentContext();
                await Assert.That(logEvents).Contains(e =>
                    e.Level == LogEventLevel.Warning &&
                    e.MessageTemplate.Text == "Circuit breaker tripped for topic {Topic}; suppressing publish for {CooldownCount} cooldown cycle(s)" &&
                    e.Properties["Topic"].ToString() == "\"healthy.topic\"" &&
                    e.Properties["CooldownCount"].ToString() == "3");
            }
        }
    }
}
