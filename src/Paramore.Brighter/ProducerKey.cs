#region Licence
/* The MIT License (MIT)
Copyright © 2025 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using System;

namespace Paramore.Brighter;

/// <summary>
/// A producer key is used to identify a producer in the <see cref="ProducerRegistry"/>.
/// </summary>
public sealed class ProducerKey
{
    /// <summary>
    /// A producer key is used to identify a producer in the <see cref="ProducerRegistry"/>.
    /// </summary>
    public ProducerKey(RoutingKey routingKey, CloudEventsType? type = null)
    {
        RoutingKey = routingKey;
        Type = type ?? CloudEventsType.Empty;
    }

    /// <summary>
    /// Gets the routing key for the producer.
    /// </summary>
    /// <value>The routing key as a <see cref="RoutingKey"/>.</value>
    public RoutingKey RoutingKey { get; }

    /// <summary>
    /// Gets the request type for the producer.
    /// </summary>
    /// <value>The request type as a <see cref="CloudEventsType"/>.</value>
    public CloudEventsType Type { get; }

    public override string ToString() => $"{RoutingKey}:{Type}";

    public override bool Equals(object? obj)
    {
        if (obj is ProducerKey other)
        {
            return RoutingKey.Equals(other.RoutingKey) && Type.Equals(other.Type);
        }
        return false;
    }

    public override int GetHashCode() => HashCode.Combine(RoutingKey, Type);
}
