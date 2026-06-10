#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

namespace Paramore.Brighter.BoxProvisioning
{
    /// <summary>
    /// Represents the version number of a box migration step.
    /// Wraps an <see cref="int"/> to provide compile-time type distinctness
    /// while preserving full participation in integer arithmetic and ordering
    /// through bidirectional implicit conversions.
    /// </summary>
    public record MigrationVersion : IComparable<MigrationVersion>
    {
        /// <summary>
        /// Gets the underlying integer version number.
        /// </summary>
        public int Value { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="MigrationVersion"/> with the specified version number.
        /// </summary>
        /// <param name="value">The version number.</param>
        public MigrationVersion(int value)
        {
            Value = value;
        }

        /// <summary>
        /// Compares this instance to another <see cref="MigrationVersion"/> by value.
        /// </summary>
        /// <param name="other">The other <see cref="MigrationVersion"/> to compare to.</param>
        /// <returns>A negative integer if this is less than <paramref name="other"/>, zero if equal, positive if greater.</returns>
        public int CompareTo(MigrationVersion? other) => Value.CompareTo(other?.Value ?? 0);

        /// <summary>
        /// Implicitly converts a <see cref="MigrationVersion"/> to its underlying <see cref="int"/> value.
        /// </summary>
        /// <param name="v">The <see cref="MigrationVersion"/> to convert.</param>
        /// <returns>The underlying integer version number.</returns>
        public static implicit operator int(MigrationVersion v) => v.Value;

        /// <summary>
        /// Implicitly converts an <see cref="int"/> to a <see cref="MigrationVersion"/>.
        /// </summary>
        /// <param name="v">The integer version number to wrap.</param>
        /// <returns>A new <see cref="MigrationVersion"/> wrapping <paramref name="v"/>.</returns>
        public static implicit operator MigrationVersion(int v) => new(v);

        /// <summary>
        /// Returns the string representation of the underlying version number.
        /// </summary>
        /// <returns>The version number as a string.</returns>
        public override string ToString() => Value.ToString();
    }
}
