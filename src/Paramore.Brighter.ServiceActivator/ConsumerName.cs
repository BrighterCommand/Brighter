#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

namespace Paramore.Brighter.ServiceActivator
{
    /// <summary>
    /// Class SubscriptionName.
    /// Value type that stores the name of a subscription. Immutable.
    /// </summary>
    public class ConsumerName : IEquatable<ConsumerName>
    {
        private readonly string _name;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConsumerName"/> class.
        /// </summary>
        /// <param name="name">The name.</param>
        public ConsumerName(string name)
        {
            _name = name;
        }

        /// <summary>
        /// Gets the subscription name as a string.
        /// </summary>
        /// <value>The value.</value>
        public string Value
        {
            get { return _name; }
        }

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>A <see cref="System.String" /> that represents this instance.</returns>
        public override string ToString()
        {
            return _name;
        }

        /// <summary>
        /// Performs an implicit conversion from <see cref="SubscriptionName"/> to <see cref="System.String"/>.
        /// </summary>
        /// <param name="rhs">The RHS.</param>
        /// <returns>The result of the conversion.</returns>
        public static implicit operator string (ConsumerName rhs)
        {
            return rhs.ToString();
        }

        /// <summary>
        /// Does the subscription name match?
        /// </summary>
        /// <param name="other">An object to compare with this object.</param>
        /// <returns>true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.</returns>
        public bool Equals(ConsumerName other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(_name, other._name);
        }

        /// <summary>
        /// Does the subscription name match?
        /// </summary>
        /// <param name="obj">The object to compare with the current object.</param>
        /// <returns><c>true</c> if the specified <see cref="System.Object" /> is equal to this instance; otherwise, <c>false</c>.</returns>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ConsumerName)obj);
        }

        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <returns>A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table.</returns>
        public override int GetHashCode()
        {
            return (_name != null ? _name.GetHashCode() : 0);
        }

        /// <summary>
        /// Implements the ==. Does the subscription name match?
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>The result of the operator.</returns>
        public static bool operator ==(ConsumerName left, ConsumerName right)
        {
            return Equals(left, right);
        }

        /// <summary>
        /// Implements the !=. Does the subscription name not match?
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>The result of the operator.</returns>
        public static bool operator !=(ConsumerName left, ConsumerName right)
        {
            return !Equals(left, right);
        }
    }
}