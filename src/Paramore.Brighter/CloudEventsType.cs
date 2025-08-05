#region License

/* The MIT License (MIT)
Copyright © 2024 Ian Cooper ian.cooper@brightercommand.com

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

namespace Paramore.Brighter;

/// <summary>
/// Represents the CloudEvents "type" attribute, describing the event's semantic meaning.
/// </summary>
/// <remarks>
/// See: https://github.com/cloudevents/spec/blob/v1.0.2/cloudevents/spec.md#type
/// </remarks>
public sealed class CloudEventsType : IEquatable<CloudEventsType>
{
    private readonly string value;

    /// <summary>
    /// Initializes a new instance of the <see cref="CloudEventsType"/> class.
    /// </summary>
    /// <param name="value">The event type, must be non-null and non-empty.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="value"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="value"/> is empty or whitespace.</exception>
    public CloudEventsType(string value)
    {
        this.value = value ?? throw new ArgumentNullException(nameof(value));
    }
    
    /// <summary>
    /// Returns an empty <see cref="CloudEventsType"/> instance.
    /// </summary>
    public static CloudEventsType Empty { get; } = new CloudEventsType(string.Empty);

    /// <summary>
    /// Gets the string value of the CloudEvents type.
    /// </summary>
    /// <value>The event type as a <see cref="string"/>.</value>
    public string Value => value;

    /// <summary>
    /// Implicitly converts a <see cref="CloudEventsType"/> to a <see cref="string"/>.
    /// </summary>
    /// <param name="type">The <see cref="CloudEventsType"/> instance.</param>
    /// <returns>The string value of the CloudEvents type.</returns>
    public static implicit operator string(CloudEventsType type) => type.value;

    /// <summary>
    /// Explicitly converts a <see cref="string"/> to a <see cref="CloudEventsType"/>.
    /// </summary>
    /// <param name="value">The string value to convert.</param>
    /// <returns>A new <see cref="CloudEventsType"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="value"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="value"/> is empty or whitespace.</exception>
    public static explicit operator CloudEventsType(string value) => new CloudEventsType(value);

    /// <inheritdoc />
    public override string ToString() => value;

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as CloudEventsType);

    /// <inheritdoc />
    public bool Equals(CloudEventsType? other) => other is not null && value == other.value;

    /// <inheritdoc />
    public override int GetHashCode() => value.GetHashCode();

    public static bool operator ==(CloudEventsType? left, CloudEventsType? right) =>
        Equals(left, right);

    public static bool operator !=(CloudEventsType? left, CloudEventsType? right) =>
        !Equals(left, right);
}
