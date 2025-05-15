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
    /// The name of a Routing Key used to wrap communication with a Broker
    /// </summary>
    public class RoutingKey
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RoutingKey"/> class.
        /// </summary>
        /// <param name="name">The name.</param>
        public RoutingKey(string name)
        {
            Value = name;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RoutingKey"/> class from a <see cref="ChannelName"/>
        /// </summary>
        /// <remarks>Use this constructor in point-to-point scenarios where you are sending directly to a queue, not via a topic</remarks>
        /// <param name="channelName">The channel that we intend to route messages to</param>
        public RoutingKey(ChannelName channelName)
        {
            Value = channelName.Value;
        }

        /// <summary>
        /// Create a null object or Empty routing key
        /// </summary>
        /// <value></value>
        public static RoutingKey Empty => new(string.Empty);

        /// <summary>
        /// Tests for an empty routing key
        /// </summary>
        /// <param name="routingKey">The routing key to test</param>
        /// <returns></returns>
        public static bool IsNullOrEmpty([NotNullWhen(false)]RoutingKey? routingKey)
        {
            return routingKey is null || string.IsNullOrEmpty(routingKey.Value);
        }

        /// <summary>
        /// Gets the name of the channel as a string.
        /// </summary>
        /// <value>The value.</value>
        public string Value { get; }

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>A <see cref="System.String" /> that represents this instance.</returns>
        public override string ToString()
        {
            return Value;
        }

        /// <summary>
        /// Performs an implicit conversion from <see cref="RoutingKey"/> to <see cref="System.String"/>.
        /// </summary>
        /// <param name="rhs">The RHS.</param>
        /// <returns>The result of the conversion.</returns>
        public static implicit operator string(RoutingKey rhs)
        {
            return rhs.ToString();
        }

        /// <summary>
        /// Performs an implicit conversion from <see cref="System.String"/> to <see cref="RoutingKey"/>.
        /// </summary>
        /// <param name="rhs">The RHS.</param>
        /// <returns>The result of the conversion.</returns>
        public static implicit operator RoutingKey(string rhs)
        {
            return new RoutingKey(rhs);
        }


        /// <summary>
        /// Do the routing key name's match?
        /// </summary>
        /// <param name="other">The other.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
        public bool Equals(RoutingKey other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(Value, other.Value);
        }

        /// <summary>
        /// Do the channel name's match?
        /// </summary>
        /// <param name="obj">The object to compare with the current object.</param>
        /// <returns><c>true</c> if the specified <see cref="System.Object" /> is equal to this instance; otherwise, <c>false</c>.</returns>
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
        /// Implements the ==. Do the channel name's match?
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>The result of the operator.</returns>
        public static bool operator ==(RoutingKey left, RoutingKey right)
        {
            return Equals(left, right);
        }

        /// <summary>
        /// Implements the !=. Do the channel name's not match?
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>The result of the operator.</returns>
        public static bool operator !=(RoutingKey? left, RoutingKey? right)
        {
            return !Equals(left, right);
        }
    }
}
