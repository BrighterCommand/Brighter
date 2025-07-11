using System.Collections.Generic;
using System.Linq;

namespace Paramore.Brighter.CircuitBreaker;

public class InMemoryCircuitBreaker(CircuitBreakerOptions? options = null) : IAmACircuitBreaker
{
    private readonly CircuitBreakerOptions _circuitBreakerOptions = options ?? new CircuitBreakerOptions();

    private readonly Dictionary<string, int> _trippedTopics = new();

    public string[] TrippedTopics => _trippedTopics.Keys.ToArray();

    public void CoolDown()
    {
        foreach (var trippedTopicsKey in _trippedTopics.Keys)
        {
            _trippedTopics[trippedTopicsKey] -= 1;

            if (_trippedTopics[trippedTopicsKey] < 0)
                _trippedTopics.Remove(trippedTopicsKey);
        }
    }

    public void TripTopic(string topic)
        => _trippedTopics[topic] = _circuitBreakerOptions.CooldownCount;
}

