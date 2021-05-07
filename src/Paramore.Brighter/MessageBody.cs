﻿#region Licence
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
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;

namespace Paramore.Brighter
{
    /// <summary>
    /// Class MessageBody
    /// The body of a <see cref="Message"/>
    /// </summary>
    public class MessageBody : IEquatable<MessageBody>
    {
        /// <summary>
        /// The message body as a byte array.
        /// </summary>
        public byte[] Bytes { get; private set; }

        /// <summary>
        /// The type of message encoded into Bytes.  A hint for deserialization that 
        /// will be sent with the byte[] to allow 
        /// </summary>
        public string BodyType { get; private set; }

        /// <summary>
        /// The message body as a string - usually used to store the message body as JSON or XML.
        /// </summary>
        /// <value>The value.</value>
        public string Value
        {
            get
            {
                return Encoding.UTF8.GetString(Bytes);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageBody"/> class with a string.  Use Value property to retrieve.
        /// </summary>
        /// <param name="body">The body of the message, usually XML or JSON.</param>
        /// <param name="bodyType">The type of the message, usualy XML or JSON. Defaults to JSON</param>
        public MessageBody(string body, string bodyType = "JSON")
        {
            Bytes = Encoding.UTF8.GetBytes(body);
            BodyType = bodyType;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageBody"/> class using a byte array.
        /// </summary>
        /// <param name="bytes">The Body of the Message</param>
        /// <param name="bodyType">Hint for deserilization, the type of message encoded in body</param>
        [JsonConstructor]
        public MessageBody(byte[] bytes, string bodyType)
        {
            Bytes = bytes;
            BodyType = bodyType ?? "JSON";
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageBody"/> class using a byte array.
        /// TODO: We don't support the range of options on Span<T> on netstandard2.0 that let's us
        /// flow through a ReadOnlyMemory<byte> for serialization so we allocate here as well as in
        /// PullConsumer when we probably don't need this allocation.
        /// We can fix in .NET 5.0 over the dead-end fork of netstandard2.1
        /// </summary>
        /// <param name="body"></param>
        /// <param name="bodyType"></param>
        public MessageBody(in ReadOnlyMemory<byte> body, string bodyType)
        {
            Bytes = body.ToArray();
            BodyType = bodyType ?? "JSON";
         }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <param name="other">An object to compare with this object.</param>
        /// <returns>true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.</returns>
        public bool Equals(MessageBody other)
        {
            return Bytes.SequenceEqual(other.Bytes) && BodyType.Equals(other.BodyType);
        }

        /// <summary>
        /// Determines whether the specified <see cref="System.Object" /> is equal to this instance.
        /// </summary>
        /// <param name="obj">The object to compare with the current object.</param>
        /// <returns><c>true</c> if the specified <see cref="System.Object" /> is equal to this instance; otherwise, <c>false</c>.</returns>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((MessageBody)obj);
        }

        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <returns>A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table.</returns>
        public override int GetHashCode()
        {
            return (Bytes != null ? Bytes.GetHashCode() : 0);
        }

        /// <summary>
        /// Implements the ==.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>The result of the operator.</returns>
        public static bool operator ==(MessageBody left, MessageBody right)
        {
            return Equals(left, right);
        }

        /// <summary>
        /// Implements the !=.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>The result of the operator.</returns>
        public static bool operator !=(MessageBody left, MessageBody right)
        {
            return !Equals(left, right);
        }
    }
}
