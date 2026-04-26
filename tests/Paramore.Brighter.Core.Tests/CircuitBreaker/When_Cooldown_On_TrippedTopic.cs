using Paramore.Brighter.CircuitBreaker;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker
{
    public class CircuitBreakerTests
    {
        [Test]
        public async Task When_TripTopic_Then_TrippedTopics_Must_Contain_Topic()
        {
            // Arrange
            var trippedTopic = new RoutingKey("topic");
            var circuitBreaker = new InMemoryOutboxCircuitBreaker(new OutboxCircuitBreakerOptions() { CooldownCount = 1 });
            // Act
            circuitBreaker.TripTopic(trippedTopic);
            // Assert
            await Assert.That(circuitBreaker.TrippedTopics).Contains(trippedTopic);
        }

        [Test]
        public async Task When_Cooldown_Decrements_CooldownCount_Then_TrippedTopicRemoved()
        {
            // Arrange
            var trippedTopic = new RoutingKey("topic");
            var circuitBreaker = new InMemoryOutboxCircuitBreaker(new OutboxCircuitBreakerOptions() { CooldownCount = 1 });
            circuitBreaker.TripTopic(trippedTopic);
            // Act
            circuitBreaker.CoolDown();
            circuitBreaker.CoolDown();
            // Assert
            await Assert.That(circuitBreaker.TrippedTopics).IsEmpty();
        }
    }
}