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

using System.Diagnostics.CodeAnalysis;

namespace Paramore.Brighter
{
    /// <summary>
    /// The name of a channel used to wrap communication with a Broker.
    /// </summary>
    /// <remarks>
    /// Channel names typically represent queue names or other addressing mechanisms 
    /// used by messaging infrastructure to route messages.
    /// </remarks>
    public class ChannelName
    {
        private readonly string _name;

        /// <summary>
        /// Initializes a new instance of the <see cref="ChannelName"/> class.
        /// </summary>
        /// <param name="name">The <see cref="string"/> name of the channel.</param>
        public ChannelName(string name)
        {
            _name = name;
        }

        /// <summary>
        /// Gets the name of the channel as a string.
        /// </summary>
        /// <value>The <see cref="string"/> value of the channel name.</value>
        public string Value
        {
            get { return _name; }
        }

        /// <summary>
        /// Returns a <see cref="string" /> that represents this instance.
        /// </summary>
        /// <returns>A <see cref="string" /> containing the channel name.</returns>
        public override string ToString()
        {
            return _name;
        }

        /// <summary>
        /// Performs an implicit conversion from <see cref="ChannelName"/> to <see cref="string"/>.
        /// </summary>
        /// <param name="rhs">The <see cref="ChannelName"/> to convert.</param>
        /// <returns>The <see cref="string"/> result of the conversion.</returns>
        public static implicit operator string?(ChannelName rhs)
        {
            return rhs?.ToString();
        }

        /// <summary>
        /// Performs an implicit conversion from <see cref="string"/> to <see cref="ChannelName"/>.
        /// </summary>
        /// <param name="rhs">The <see cref="string"/> to convert.</param>
        /// <returns>The <see cref="ChannelName"/> result of the conversion.</returns>
        public static implicit operator ChannelName(string rhs)
        {
            return new ChannelName(rhs);
        }

        /// <summary>
        /// Determines whether the channel names match.
        /// </summary>
        /// <param name="other">The other <see cref="ChannelName"/> to compare.</param>
        /// <returns><c>true</c> if the channel names are equal; otherwise, <c>false</c>.</returns>
        public bool Equals(ChannelName other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(_name, other._name);
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
            return Equals((ChannelName)obj);
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
        /// Implements the == operator. Determines whether the channel names match.
        /// </summary>
        /// <param name="left">The left <see cref="ChannelName"/>.</param>
        /// <param name="right">The right <see cref="ChannelName"/>.</param>
        /// <returns><c>true</c> if the channel names are equal; otherwise, <c>false</c>.</returns>
        public static bool operator ==(ChannelName? left, ChannelName? right)
        {
            return Equals(left, right);
        }

        /// <summary>
        /// Implements the != operator. Determines whether the channel names do not match.
        /// </summary>
        /// <param name="left">The left <see cref="ChannelName"/>.</param>
        /// <param name="right">The right <see cref="ChannelName"/>.</param>
        /// <returns><c>true</c> if the channel names are not equal; otherwise, <c>false</c>.</returns>
        public static bool operator !=(ChannelName? left, ChannelName? right)
        {
            return !Equals(left, right);
        }

        /// <summary>
        /// Determines whether the specified channel name is null or empty.
        /// </summary>
        /// <param name="channelName">The <see cref="ChannelName"/> to test.</param>
        /// <returns><c>true</c> if the channel name is null or empty; otherwise, <c>false</c>.</returns>
        public static bool IsNullOrEmpty([NotNullWhen(false)]ChannelName? channelName)
        {
            return channelName is not null && string.IsNullOrEmpty(channelName._name);
        }
    }
}
