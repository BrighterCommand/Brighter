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
using System.Net.Mime;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using Paramore.Brighter.Extensions;

namespace Paramore.Brighter
{
    /// <summary>
    /// Class MessageBody
    /// The body of a <see cref="Message"/>
    /// </summary>
    public class MessageBody : IEquatable<MessageBody>
    {
        private readonly ReadOnlyMemory<byte> _memory;
        private string? _cachedValue;
        private int _cachedHashCode;
        private bool _hashCodeComputed;

        /// <summary>
        /// The message body as a byte array.
        /// Allocates a new array on every call. Prefer <see cref="Memory"/> for zero-copy access.
        /// </summary>
        [Obsolete("Use Memory for zero-copy access. This property allocates on every call.")]
        public byte[] Bytes => _memory.ToArray();

        /// <summary>
        /// The message body as a <see cref="ReadOnlyMemory{T}"/> of <see cref="byte"/>.
        /// Zero-copy access to the body content.
        /// </summary>
        public ReadOnlyMemory<byte> Memory => _memory;

        /// <summary>
        /// Returns the underlying byte[] without copying when the memory is backed by an array.
        /// Falls back to allocating a new array when the memory is not array-backed.
        /// Use this when an API requires byte[] and <see cref="Memory"/> or Span cannot be used.
        /// </summary>
        public byte[] ToByteArray()
        {
            if (MemoryMarshal.TryGetArray(Memory, out ArraySegment<byte> segment)
                && segment.Offset == 0
                && segment.Count == segment.Array!.Length)
            {
                return segment.Array;
            }

            return Memory.ToArray();
        }

        /// <summary>
        /// The type of message encoded into Bytes.  A hint for deserialization that 
        /// will be sent with the byte[] to allow
        /// </summary>
        public ContentType? ContentType { get; set; }

        /// <summary>
        /// What is  the character encoding of the text in the message
        /// </summary>
        public CharacterEncoding CharacterEncoding { get; private set; }

        /// <summary>
        /// The message body as a string.
        /// If the message body is UTF8 or ASCII, this will be the same as the string representation of the byte array.
        /// If the message body is Base64 encoded, this will be the encoded string.
        /// if the message body is compressed, it will throw an exception, you should decompress the bytes first. 
        /// </summary>
        /// <value>The value.</value>
        public string Value
        {
            get
            {
                var cached = Volatile.Read(ref _cachedValue);
                if (cached is not null) return cached;

                var result = CharacterEncoding switch
                {
                    CharacterEncoding.Base64 => ToBase64(),
                    CharacterEncoding.Raw => ToBase64(),
                    CharacterEncoding.UTF8 => DecodeString(Encoding.UTF8),
                    CharacterEncoding.ASCII => DecodeString(Encoding.ASCII),
                    _ => throw new InvalidCastException(
                        $"Message Body with {CharacterEncoding} is not available")
                };

                Volatile.Write(ref _cachedValue, result);
                return result;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageBody"/> class with a string.  Use Value property to retrieve.
        /// </summary>
        /// <param name="body">The <see cref="string"/> body of the message, usually XML or JSON.</param>
        /// <param name="contentType">The <see cref="ContentType"/> of the message, usually "application/json". Defaults to "application/json".</param>
        /// <param name="characterEncoding">The <see cref="CharacterEncoding"/> of the content. Defaults to <see cref="CharacterEncoding.UTF8"/>.
        /// If you pass us "application/octet" but the type is ascii or utf8, we will convert to base64 for you.
        /// </param>
        public MessageBody(string? body, ContentType? contentType = null, CharacterEncoding characterEncoding = CharacterEncoding.UTF8)
        {
#if NETSTANDARD2_0
            ContentType = contentType ?? new ContentType("application/json");
            SetCharacterEncoding(ContentType, characterEncoding);
#else            
            ContentType = contentType ?? new ContentType(MediaTypeNames.Application.Json);
            SetCharacterEncoding(ContentType, characterEncoding);
#endif
            CharacterEncoding = characterEncoding;
            
            if (characterEncoding == CharacterEncoding.Raw) throw new ArgumentOutOfRangeException("characterEncoding", "Raw encoding is not supported for string constructor");

            if (body == null)
            {
                _memory = ReadOnlyMemory<byte>.Empty;
                return;
            }

            _memory = CharacterEncoding switch
            {
                CharacterEncoding.Base64 => Convert.FromBase64String(body),
                CharacterEncoding.UTF8 => Encoding.UTF8.GetBytes(body),
                CharacterEncoding.ASCII => Encoding.ASCII.GetBytes(body),
                _ => ReadOnlyMemory<byte>.Empty
            };
        }



        /// <summary>
        /// Initializes a new instance of the <see cref="MessageBody"/> class using a byte array.
        /// </summary>
        /// <param name="bytes">The <see cref="byte"/> array containing the body of the message.</param>
        /// <param name="contentType">The <see cref="ContentType"/> of message encoded in body.</param>
        /// <param name="characterEncoding">The <see cref="CharacterEncoding"/> used for any text content in the body.</param>
        [JsonConstructor]
        public MessageBody(byte[]? bytes, ContentType? contentType = null,  CharacterEncoding characterEncoding = CharacterEncoding.UTF8)
        {
#if NETSTANDARD2_0
            ContentType = contentType ?? new ContentType("application/json");
            SetCharacterEncoding(ContentType, characterEncoding);
#else            
            ContentType = contentType ?? new ContentType(MediaTypeNames.Application.Json);
            SetCharacterEncoding(ContentType, characterEncoding);
#endif
            CharacterEncoding = characterEncoding;
            
            if (bytes is null)
            {
                _memory = ReadOnlyMemory<byte>.Empty;
                return;
            }

            _memory = bytes;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageBody"/> class using a <see cref="ReadOnlyMemory{T}"/> of <see cref="byte"/>.
        /// Zero-copy: the memory is stored directly without allocation.
        /// </summary>
        /// <param name="body">The <see cref="ReadOnlyMemory{T}"/> of <see cref="byte"/> containing the body of the message.</param>
        /// <param name="contentType">The <see cref="ContentType"/> of the body.</param>
        /// <param name="characterEncoding">The <see cref="CharacterEncoding"/> of any text in the body.</param>
        public MessageBody(in ReadOnlyMemory<byte> body, ContentType? contentType = null, CharacterEncoding characterEncoding = CharacterEncoding.UTF8)
        {
#if NETSTANDARD2_0
            ContentType = contentType ?? new ContentType("application/json");
            SetCharacterEncoding(ContentType, characterEncoding);
#else            
            ContentType = contentType ?? new ContentType(MediaTypeNames.Application.Json);
            SetCharacterEncoding(ContentType, characterEncoding);
#endif
            _memory = body;
            CharacterEncoding = characterEncoding;
        }


        /// <summary>
        /// Converts the body to a character-encoded string using the specified encoding.
        /// </summary>
        /// <param name="characterEncoding">The <see cref="CharacterEncoding"/> to use for conversion.</param>
        /// <returns>A <see cref="string"/> representation of the message body using the specified encoding.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the specified encoding is not supported for string conversion.</exception>
        public string ToCharacterEncodedString(CharacterEncoding characterEncoding)
        {
            return characterEncoding switch
            {
                CharacterEncoding.Base64 => ToBase64(),
                CharacterEncoding.UTF8 => DecodeString(Encoding.UTF8),
                CharacterEncoding.ASCII => DecodeString(Encoding.ASCII),
                _ => throw new InvalidOperationException($"Message Body with {CharacterEncoding} is not available")
            };
        }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <param name="other">An object to compare with this object.</param>
        /// <returns>true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.</returns>
        public bool Equals(MessageBody? other)
        {
            if (other is null) return false;
            var bodyEqual = _memory.Span.SequenceEqual(other._memory.Span);
            var sameContentType = ContentType is null || ContentType.Equals(other.ContentType);
            return bodyEqual && sameContentType ;
        }

        /// <summary>
        /// Determines whether the specified <see cref="System.Object" /> is equal to this instance.
        /// </summary>
        /// <param name="obj">The object to compare with the current object.</param>
        /// <returns><c>true</c> if the specified <see cref="System.Object" /> is equal to this instance; otherwise, <c>false</c>.</returns>
        public override bool Equals(object? obj)
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
            if (Volatile.Read(ref _hashCodeComputed)) return _cachedHashCode;

            var hash = new HashCode();
#if NET6_0_OR_GREATER // AddBytes requires .NET 6+, not just non-netstandard2.0
            hash.AddBytes(_memory.Span);
#else
            foreach (var b in _memory.Span)
                hash.Add(b);
#endif
            hash.Add(ContentType?.ToString());
            var result = hash.ToHashCode();
            Volatile.Write(ref _cachedHashCode, result);
            Volatile.Write(ref _hashCodeComputed, true);
            return result;
        }

        /// <summary>
        /// Implements the ==.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>The result of the operator.</returns>
        public static bool operator ==(MessageBody? left, MessageBody? right)
        {
            return Equals(left, right);
        }

        /// <summary>
        /// Implements the !=.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>The result of the operator.</returns>
        public static bool operator !=(MessageBody? left, MessageBody? right)
        {
            return !Equals(left, right);
        }
        
        private void SetCharacterEncoding(ContentType? contentType, CharacterEncoding characterEncoding)
        {
            var characterEncodingString = characterEncoding.FromCharacterEncoding();
            if (contentType is not null && characterEncodingString is not null)
                contentType.CharSet = characterEncodingString;
        }

#if NETSTANDARD2_0
        private string ToBase64() => Convert.ToBase64String(_memory.ToArray());
        private string DecodeString(Encoding encoding) => encoding.GetString(_memory.ToArray());
#else
        private string ToBase64() => Convert.ToBase64String(_memory.Span);
        private string DecodeString(Encoding encoding) => encoding.GetString(_memory.Span);
#endif
    }
}
