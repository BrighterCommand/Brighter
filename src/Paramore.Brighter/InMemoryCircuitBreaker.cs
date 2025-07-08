using System.Collections.Generic;
using System.Linq;

namespace Paramore.Brighter;

public class InMemoryCircuitBreaker : IAmACircuitBreaker
{
    private static readonly Dictionary<string, int> s_trippedTopics = new();

    public string[] TrippedTopics => s_trippedTopics.Keys.ToArray();

    public void CoolDown()
    {
        foreach (var trippedTopicsKey in s_trippedTopics.Keys)
        {
            CoolDownTopic(trippedTopicsKey);
        }
    }

    public void TripTopic(string topic, int coolDownCount = 1)
        => s_trippedTopics[topic] = coolDownCount;

    private static void CoolDownTopic(string trippedTopicsKey)
    {
        int coolDownCount = s_trippedTopics[trippedTopicsKey];
        coolDownCount = coolDownCount - 1;
        s_trippedTopics[trippedTopicsKey] = coolDownCount;

        if (coolDownCount < 0)
            s_trippedTopics.Remove(trippedTopicsKey);
        else
            s_trippedTopics[trippedTopicsKey] = coolDownCount;
    }
}

