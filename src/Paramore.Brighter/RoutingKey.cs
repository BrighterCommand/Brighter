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

using System.Diagnostics.CodeAnalysis;

namespace Paramore.Brighter
{
    /// <summary>
    /// The name of a Routing Key used to wrap communication with a Broker.
    /// </summary>
    /// <remarks>
    /// Routing keys are used in pub-sub messaging to determine how messages are routed
    /// from publishers to subscribers through the broker infrastructure.
    /// </remarks>
    public class RoutingKey
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RoutingKey"/> class.
        /// </summary>
        /// <param name="name">The <see cref="string"/> name of the routing key.</param>
        public RoutingKey(string name)
        {
            Value = name;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RoutingKey"/> class from a <see cref="ChannelName"/>.
        /// </summary>
        /// <param name="channelName">The <see cref="ChannelName"/> that we intend to route messages to.</param>
        /// <remarks>Use this constructor in point-to-point scenarios where you are sending directly to a queue, not via a topic.</remarks>
        public RoutingKey(ChannelName channelName)
        {
            Value = channelName.Value;
        }

        /// <summary>
        /// Create a null object or Empty routing key.
        /// </summary>
        /// <value>An empty <see cref="RoutingKey"/> instance.</value>
        public static RoutingKey Empty => new(string.Empty);

        /// <summary>
        /// Tests for an empty routing key.
        /// </summary>
        /// <param name="routingKey">The <see cref="RoutingKey"/> to test.</param>
        /// <returns><c>true</c> if the <paramref name="routingKey"/> is null or empty; otherwise, <c>false</c>.</returns>
        public static bool IsNullOrEmpty([NotNullWhen(false)]RoutingKey? routingKey)
        {
            return routingKey is null || string.IsNullOrEmpty(routingKey.Value);
        }

        /// <summary>
        /// Gets the name of the channel as a string.
        /// </summary>
        /// <value>The <see cref="string"/> value of the routing key.</value>
        public string Value { get; }

        /// <summary>
        /// Returns a <see cref="string" /> that represents this instance.
        /// </summary>
        /// <returns>A <see cref="string" /> containing the routing key value.</returns>
        public override string ToString()
        {
            return Value;
        }

        /// <summary>
        /// Performs an implicit conversion from <see cref="RoutingKey"/> to <see cref="string"/>.
        /// </summary>
        /// <param name="rhs">The <see cref="RoutingKey"/> to convert.</param>
        /// <returns>The <see cref="string"/> result of the conversion.</returns>
        public static implicit operator string(RoutingKey rhs)
        {
            return rhs.ToString();
        }

        /// <summary>
        /// Performs an implicit conversion from <see cref="string"/> to <see cref="RoutingKey"/>.
        /// </summary>
        /// <param name="rhs">The <see cref="string"/> to convert.</param>
        /// <returns>The <see cref="RoutingKey"/> result of the conversion.</returns>
        public static implicit operator RoutingKey(string rhs)
        {
            return new RoutingKey(rhs);
        }


        /// <summary>
        /// Determines whether the routing key names match.
        /// </summary>
        /// <param name="other">The other <see cref="RoutingKey"/> to compare.</param>
        /// <returns><c>true</c> if the routing keys are equal; otherwise, <c>false</c>.</returns>
        public bool Equals(RoutingKey? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(Value, other.Value);
        }

        /// <summary>
        /// Determines whether the channel names match.
        /// </summary>
        /// <param name="obj">The <see cref="object"/> to compare with the current object.</param>
        /// <returns><c>true</c> if the specified <see cref="object" /> is equal to this instance; otherwise, <c>false</c>.</returns>
        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((RoutingKey)obj);
        }

        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <returns>A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table.</returns>
        public override int GetHashCode()
        {
            return Value?.GetHashCode() ?? 0;
        }

        /// <summary>
        /// Implements the == operator. Determines whether the channel names match.
        /// </summary>
        /// <param name="left">The left <see cref="RoutingKey"/>.</param>
        /// <param name="right">The right <see cref="RoutingKey"/>.</param>
        /// <returns><c>true</c> if the routing keys are equal; otherwise, <c>false</c>.</returns>
        public static bool operator ==(RoutingKey? left, RoutingKey? right)
        {
            return Equals(left, right);
        }

        /// <summary>
        /// Implements the != operator. Determines whether the channel names do not match.
        /// </summary>
        /// <param name="left">The left <see cref="RoutingKey"/>.</param>
        /// <param name="right">The right <see cref="RoutingKey"/>.</param>
        /// <returns><c>true</c> if the routing keys are not equal; otherwise, <c>false</c>.</returns>
        public static bool operator !=(RoutingKey? left, RoutingKey? right)
        {
            return !Equals(left, right);
        }
    }
}
