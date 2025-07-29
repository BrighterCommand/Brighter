#region Licence
/* The MIT License (MIT)
Copyright © 2025 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Diagnostics.CodeAnalysis;

namespace Paramore.Brighter
{
    /// <summary>
    /// Represents a value type for identifiers, providing stronger typing than primitive strings.
    /// Used as a base type for various identifiers in the system.
    /// </summary>
    /// <remarks>
    /// Id wraps a string value and provides implicit conversion operators to maintain compatibility
    /// with existing string-based code while offering better type safety and domain semantics.
    /// </remarks>
    public record Id
    {
        /// <summary>
        /// Gets the string representation of the identifier.
        /// </summary>
        /// <value>A <see cref="string"/> containing the identifier value.</value>
        public string Value { get; }

        /// <summary>
        /// Gets an empty Id instance.
        /// </summary>
        /// <value>An <see cref="Id"/> with an empty string value.</value>
        /// <remarks>
        ///  Should be used to indicate a lack of value or an uninitialized state.
        /// </remarks>
        public static Id Empty { get; } = new(string.Empty);

        /// <summary>
        /// Creates a new Id with a randomly assigned UUID as the key
        /// </summary>
        /// <value>An <see cref="Id"/> with a GUID value.</value>
        public static Id Random 
        {
            get => new(Uuid.NewAsString());
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Id"/> class.
        /// </summary>
        /// <param name="value">The <see cref="string"/> value of the identifier.</param>
        public Id(string value)
        {
            Value = value;
        }

        /// <summary>
        /// Creates a new Id instance with a new GUID if the provided value is null or empty.
        /// </summary>
        /// <param name="value">The <see cref="string"/> value of the Id, pass null for a random GUID.</param>
        /// <returns>A new <see cref="Id"/> instance with the provided value or a random GUID if value is null or empty.</returns>
        public static Id Create(string? value)
        {
            return new Id(value ?? Uuid.NewAsString());
        }
       
        /// <summary>
        /// Returns true if the Id is null or empty.
        /// </summary>
        /// <param name="id">The <see cref="Id"/> to test.</param>
        /// <returns><c>true</c> if the <paramref name="id"/> is null or has an empty value; otherwise, <c>false</c>.</returns>
        public static bool IsNullOrEmpty([NotNullWhen(false)] Id? id)
        {
            return id == null || string.IsNullOrEmpty(id.Value);
        }

        /// <summary>
        /// Implicitly converts an Id to its string representation.
        /// </summary>
        /// <param name="id">The <see cref="Id"/> to convert.</param>
        /// <returns>The <see cref="string"/> value of the identifier.</returns>
        public static implicit operator string(Id id) => id.Value;

        /// <summary>
        /// Implicitly converts a string to an Id.
        /// </summary>
        /// <param name="value">The <see cref="string"/> to convert.</param>
        /// <returns>A new <see cref="Id"/> instance with the provided value.</returns>
        public static implicit operator Id(string value) => new(value);

        /// <summary>
        /// Returns the string representation of the identifier.
        /// </summary>
        /// <returns>The identifier's <see cref="string"/> value.</returns>
        public override string ToString() => Value;
    }
}
