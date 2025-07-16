using System.Collections.Generic;
using System.Linq;

namespace Paramore.Brighter.CircuitBreaker;

/// <summary>
/// In-memory implementation of IAmAnOutboxCircuitBreaker; Tracks dispatch failures per topic,
/// if a topic is marked as tripped, record that topic as a failure and cooldown by decrementing
/// cool down count.
/// </summary>
/// <param name="options"></param>
public class InMemoryOutboxCircuitBreaker(OutboxCircuitBreakerOptions? options = null) : IAmAnOutboxCircuitBreaker
{
    private readonly OutboxCircuitBreakerOptions _outboxCircuitBreakerOptions = options ?? new OutboxCircuitBreakerOptions();

    private readonly Dictionary<RoutingKey, int> _trippedTopics = new();

    /// <summary>
    /// A collection of tripped topics.
    /// </summary>
    public IEnumerable<RoutingKey> TrippedTopics => _trippedTopics.Keys.ToArray();


    /// <summary>
    /// Each time an attempt to ClearOutstandingFromOutbox the IAmAnOutboxCircuitBreaker is cooled down,
    /// this decrements the tripped topics, making them available for publication once the Cooldown
    /// period is set to zero.
    /// </summary>
    public void CoolDown()
    {
        foreach (var trippedTopicsKey in _trippedTopics.Keys)
        {
            _trippedTopics[trippedTopicsKey] -= 1;

            if (_trippedTopics[trippedTopicsKey] < 0)
                _trippedTopics.Remove(trippedTopicsKey);
        }
    }

    /// <summary>
    /// If a topic exceeds a configurable failure threshold within a time window, mark it as "tripped" 
    /// </summary>
    /// <param name="topic">Name of the entity to circuit break</param>
    public void TripTopic(RoutingKey topic)
        => _trippedTopics[topic] = _outboxCircuitBreakerOptions.CooldownCount;
}

