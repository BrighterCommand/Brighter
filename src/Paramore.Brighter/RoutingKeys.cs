using System.Collections;
using System.Collections.Generic;

namespace Paramore.Brighter;

public class RoutingKeys : IEnumerable<RoutingKey>
{
    private readonly IEnumerable<RoutingKey> _routingKeys;

    public RoutingKeys(params RoutingKey[] routingKeys)
    {
        _routingKeys = routingKeys;
    }

    //TODO: [MustDisposeResource]
    public IEnumerator<RoutingKey> GetEnumerator()
    {
        return _routingKeys.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public override string ToString()
    {
        return $"[{string.Join(", ", _routingKeys)}]";
    }
}
