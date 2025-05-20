#region Licence
/* The MIT License (MIT)
Copyright © 2024 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System.Collections;
using System.Collections.Generic;

namespace Paramore.Brighter;

/// <summary>
/// Represents a collection of routing keys used for message routing in a messaging system.
/// This class provides an enumerable container for multiple <see cref="RoutingKey"/> instances.
/// </summary>
/// <param name="routingKeys">An array of routing keys to initialize the collection</param>
public class RoutingKeys(params RoutingKey[] routingKeys) : IEnumerable<RoutingKey>
{
    private readonly IEnumerable<RoutingKey> _routingKeys = routingKeys;

    /// <summary>
    /// Returns an enumerator that iterates through the routing keys collection.
    /// </summary>
    /// <returns>An enumerator that can be used to iterate through the collection.</returns>
    public IEnumerator<RoutingKey> GetEnumerator()
    {
        return _routingKeys.GetEnumerator();
    }

    /// <summary>
    /// Returns an enumerator that iterates through the routing keys collection.
    /// </summary>
    /// <returns>An IEnumerator interface implementation.</returns>
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <summary>
    /// Returns a string representation of the routing keys collection.
    /// </summary>
    /// <returns>A comma-separated list of routing keys enclosed in square brackets.</returns>
    public override string ToString()
    {
        return $"[{string.Join(", ", _routingKeys)}]";
    }
}
