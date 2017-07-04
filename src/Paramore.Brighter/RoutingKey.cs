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
        public override bool Equals(object obj)
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
        public static bool operator !=(RoutingKey left, RoutingKey right)
        {
            return !Equals(left, right);
        }
    }
}
