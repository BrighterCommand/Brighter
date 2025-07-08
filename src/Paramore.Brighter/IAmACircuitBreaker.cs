using System.Collections.ObjectModel;

namespace Paramore.Brighter
{
    public interface IAmACircuitBreaker
    {
        /// <summary>
        /// Each time an attempt to ClearOutstandingFromOutbox the IAmACircuitBreaker is cooled down,
        /// this decrements the tripped topics, making them available for publication once the Cooldown
        /// period is set to zero.
        /// </summary>
        public void CoolDown();

        /// <summary>
        /// If a topic exceeds a configurable failure threshold within a time window, mark it as "tripped" 
        /// </summary>
        /// <param name="topic">Name of the entity to circuit break</param>
        /// <param name="coolDownCount">number of times to cool down</param>
        public void TripTopic(string topic, int coolDownCount);

        /// <summary>
        /// A collection of tripped topics. As the circuit breaker is injected into both mediator
        /// and stores, the list of tripped topics is required to be readonly.
        /// </summary>
        public string[] TrippedTopics { get; }
    }
}
