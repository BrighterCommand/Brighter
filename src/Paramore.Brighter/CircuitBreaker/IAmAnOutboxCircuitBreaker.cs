using System.Collections.Generic;

namespace Paramore.Brighter.CircuitBreaker
{
    public interface IAmAnOutboxCircuitBreaker
    {
        /// <summary>
        /// Each time an attempt to ClearOutstandingFromOutbox the IAmAnOutboxCircuitBreaker is cooled down,
        /// this decrements the tripped topics, making them available for publication once the Cooldown
        /// period is set to zero.
        /// </summary>
        public void CoolDown();

        /// <summary>
        /// If a topic exceeds a configurable failure threshold within a time window, mark it as "tripped" 
        /// </summary>
        /// <param name="topic">Name of the entity to circuit break</param>
        public void TripTopic(RoutingKey topic);

        /// <summary>
        /// A collection of tripped topics.
        /// </summary>
        public IEnumerable<RoutingKey> TrippedTopics { get; }
    }
}
