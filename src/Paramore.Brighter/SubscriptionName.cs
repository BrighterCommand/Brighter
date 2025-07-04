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

namespace Paramore.Brighter
{
    /// <summary>
    /// Class SubscriptionName.
    /// Value type that stores the name of a subscription. Immutable.
    /// </summary>
    /// <remarks>
    /// Subscription names are used to identify and manage individual message subscriptions
    /// within the dispatcher and service activator infrastructure.
    /// </remarks>
    public class SubscriptionName : IEquatable<SubscriptionName>
    {
        private readonly string _name;

        /// <summary>
        /// Initializes a new instance of the <see cref="SubscriptionName"/> class.
        /// </summary>
        /// <param name="name">The <see cref="string"/> name of the subscription.</param>
        public SubscriptionName(string name)
        {
            _name = name;
        }

        /// <summary>
        /// Gets the subscription name as a string.
        /// </summary>
        /// <value>The <see cref="string"/> value of the subscription name.</value>
        public string Value
        {
            get { return _name; }
        }

        /// <summary>
        /// Returns a <see cref="string" /> that represents this instance.
        /// </summary>
        /// <returns>A <see cref="string" /> containing the subscription name.</returns>
        public override string ToString()
        {
            return _name;
        }

        /// <summary>
        /// Performs an implicit conversion from <see cref="SubscriptionName"/> to <see cref="string"/>.
        /// </summary>
        /// <param name="rhs">The <see cref="SubscriptionName"/> to convert.</param>
        /// <returns>The <see cref="string"/> result of the conversion.</returns>
        public static implicit operator string (SubscriptionName rhs)
        {
            return rhs.ToString();
        }
        
        /// <summary>
        /// Performs an implicit conversion from <see cref="string"/> to <see cref="SubscriptionName"/>.
        /// </summary>
        /// <param name="rhs">The <see cref="string"/> to convert to a <see cref="SubscriptionName"/>.</param>
        /// <returns>A new <see cref="SubscriptionName"/> instance.</returns>
        public static implicit operator SubscriptionName(string rhs)
        {
            return new SubscriptionName(rhs);
        }

        /// <summary>
        /// Determines whether the subscription names match.
        /// </summary>
        /// <param name="other">An <see cref="object"/> to compare with this object.</param>
        /// <returns><c>true</c> if the current object is equal to the <paramref name="other" /> parameter; otherwise, <c>false</c>.</returns>
        public bool Equals(SubscriptionName? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(_name, other._name);
        }

        /// <summary>
        /// Determines whether the subscription names match.
        /// </summary>
        /// <param name="obj">The <see cref="object"/> to compare with the current object.</param>
        /// <returns><c>true</c> if the specified <see cref="object" /> is equal to this instance; otherwise, <c>false</c>.</returns>
        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((SubscriptionName)obj);
        }

        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <returns>A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table.</returns>
        public override int GetHashCode()
        {
            return _name.GetHashCode();
        }

        /// <summary>
        /// Implements the == operator. Determines whether the subscription names match.
        /// </summary>
        /// <param name="left">The left <see cref="SubscriptionName"/>.</param>
        /// <param name="right">The right <see cref="SubscriptionName"/>.</param>
        /// <returns><c>true</c> if the subscription names are equal; otherwise, <c>false</c>.</returns>
        public static bool operator ==(SubscriptionName? left, SubscriptionName? right)
        {
            return Equals(left, right);
        }

        /// <summary>
        /// Implements the != operator. Determines whether the subscription names do not match.
        /// </summary>
        /// <param name="left">The left <see cref="SubscriptionName"/>.</param>
        /// <param name="right">The right <see cref="SubscriptionName"/>.</param>
        /// <returns><c>true</c> if the subscription names are not equal; otherwise, <c>false</c>.</returns>
        public static bool operator !=(SubscriptionName? left, SubscriptionName? right)
        {
            return !Equals(left, right);
        }
    }
}
