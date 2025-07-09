using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Paramore.Brighter.CircuitBreaker;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker
{
    public class CircuitBreakerTests
    {
        [Fact]
        public void When_TripTopic()
        {
            // Arrange
            var trippedTopic = "topic";
            var circuitBreaker = new InMemoryCircuitBreaker(
                new CircuitBreakerOptions() { CooldownCount = 1 });
            
            // Act
            circuitBreaker.TripTopic(trippedTopic);

            // Assert
            Assert.Contains(trippedTopic, circuitBreaker.TrippedTopics);
        }

        [Fact]
        public void When_Cooldown_OnTrippedTopic()
        {
            // Arrange
            var trippedTopic = "topic";
            var circuitBreaker = new InMemoryCircuitBreaker(
                new CircuitBreakerOptions() { CooldownCount = 1 });
            circuitBreaker.TripTopic(trippedTopic);

            // Act
            circuitBreaker.CoolDown();

            // Assert
            Assert.Empty(circuitBreaker.TrippedTopics);
        }
    }
}
