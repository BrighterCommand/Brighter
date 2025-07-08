using System.Collections.Generic;
using System.Linq;
using Paramore.Brighter.CircuitBreaker;

namespace Paramore.Brighter;

public class InMemoryCircuitBreaker(CircuitBreakerOptions? options = null) : IAmACircuitBreaker
{
    private readonly CircuitBreakerOptions _circuitBreakerOptions = options ?? new CircuitBreakerOptions();

    private static readonly Dictionary<string, int> s_trippedTopics = new();

    public string[] TrippedTopics => s_trippedTopics.Keys.ToArray();

    public void CoolDown()
    {
        foreach (var trippedTopicsKey in s_trippedTopics.Keys)
        {
            s_trippedTopics[trippedTopicsKey] -= 1;

            if (s_trippedTopics[trippedTopicsKey] < 1)
                s_trippedTopics.Remove(trippedTopicsKey);
        }
    }

    public void TripTopic(string topic)
        => s_trippedTopics[topic] = _circuitBreakerOptions.CooldownCount;
}

