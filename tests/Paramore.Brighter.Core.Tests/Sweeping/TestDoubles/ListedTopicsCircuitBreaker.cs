using System.Collections.Generic;
using System.Linq;
using Paramore.Brighter.CircuitBreaker;

namespace Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;

/// <summary>
/// A third-party <see cref="IAmAnOutboxCircuitBreaker"/> that implements only the interface members and
/// reports the topics it was constructed with as tripped.
/// </summary>
public sealed class ListedTopicsCircuitBreaker(params string[] topics) : IAmAnOutboxCircuitBreaker
{
    public void CoolDown() { }

    public void TripTopic(RoutingKey topic) { }

    public IEnumerable<RoutingKey> TrippedTopics => topics.Select(topic => new RoutingKey(topic));
}
