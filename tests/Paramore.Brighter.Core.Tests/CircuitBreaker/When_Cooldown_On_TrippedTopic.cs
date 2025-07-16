using Paramore.Brighter.CircuitBreaker;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker
{
    public class CircuitBreakerTests
    {
        [Fact]
        public void When_TripTopic_Then_TrippedTopics_Must_Contain_Topic()
        {
            // Arrange
            var trippedTopic = new RoutingKey("topic");
            var circuitBreaker = new InMemoryOutboxCircuitBreaker(
                new OutboxCircuitBreakerOptions() { CooldownCount = 1 });
            
            // Act
            circuitBreaker.TripTopic(trippedTopic);

            // Assert
            Assert.Contains(trippedTopic, circuitBreaker.TrippedTopics);
        }

        [Fact]
        public void When_Cooldown_Decrements_CooldownCount_Then_TrippedTopicRemoved()
        {
            // Arrange
            var trippedTopic = new RoutingKey("topic");
            var circuitBreaker = new InMemoryOutboxCircuitBreaker(
                new OutboxCircuitBreakerOptions() { CooldownCount = 1 });
            circuitBreaker.TripTopic(trippedTopic);

            // Act
            circuitBreaker.CoolDown();
            circuitBreaker.CoolDown();

            // Assert
            Assert.Empty(circuitBreaker.TrippedTopics);
        }
    }
}
