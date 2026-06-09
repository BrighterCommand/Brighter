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

using System.Diagnostics.CodeAnalysis;

namespace Paramore.Brighter.BoxProvisioning
{
    /// <summary>
    /// Represents a source reference for a box migration step (e.g. a commit hash and PR number).
    /// Wraps a <see cref="string"/> to provide compile-time type distinctness, preventing
    /// accidental transposition with <see cref="MigrationDescription"/> or other string-typed
    /// provisioning parameters.
    /// </summary>
    /// <remarks>
    /// V1 migrations carry no source reference; <see langword="null"/> is a valid value (FR-7, D6).
    /// Use <see cref="IsNullOrEmpty"/> to guard before use.
    /// </remarks>
    public record SourceReference
    {
        /// <summary>
        /// Gets the underlying string value of the source reference.
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="SourceReference"/> with the specified reference string.
        /// </summary>
        /// <param name="value">The source reference string.</param>
        public SourceReference(string value)
        {
            Value = value;
        }

        /// <summary>
        /// Returns <see langword="true"/> if <paramref name="value"/> is <see langword="null"/> or wraps an empty string;
        /// otherwise <see langword="false"/>.
        /// </summary>
        /// <param name="value">The <see cref="SourceReference"/> to test.</param>
        /// <returns><see langword="true"/> when <paramref name="value"/> is <see langword="null"/> or empty; otherwise <see langword="false"/>.</returns>
        public static bool IsNullOrEmpty([NotNullWhen(false)] SourceReference? value)
            => value is null || string.IsNullOrEmpty(value.Value);

        /// <summary>
        /// Implicitly converts a <see cref="SourceReference"/> to its underlying <see cref="string"/> value.
        /// Returns <see langword="null"/> when the <see cref="SourceReference"/> instance is <see langword="null"/>.
        /// </summary>
        /// <param name="value">The <see cref="SourceReference"/> to convert.</param>
        /// <returns>The underlying string, or <see langword="null"/> if <paramref name="value"/> is <see langword="null"/>.</returns>
        public static implicit operator string?(SourceReference value) => value?.Value;

        /// <summary>
        /// Implicitly converts a <see cref="string"/> to a <see cref="SourceReference"/>.
        /// </summary>
        /// <param name="value">The string to wrap.</param>
        /// <returns>A new <see cref="SourceReference"/> wrapping <paramref name="value"/>.</returns>
        public static implicit operator SourceReference(string value) => new(value);

        /// <summary>
        /// Returns the underlying string value of this <see cref="SourceReference"/>.
        /// </summary>
        /// <returns>The source reference string.</returns>
        public override string ToString() => Value;
    }
}
