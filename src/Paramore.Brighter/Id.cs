using System;

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
        public string Value { get; }

        /// <summary>
        /// Gets an empty Id instance.
        /// </summary>
        /// <remarks>
        ///  Should be used to indicate a lack of value or an uninitialized state.
        /// </remarks>
        public static Id Empty { get; } = new(string.Empty);

        /// <summary>
        /// Creates a new Id with a randomly assigned UUID as the key
        /// </summary>
        public static Id Random { get; } = new(Guid.NewGuid().ToString()); 

        /// <summary>
        /// Initializes a new instance of the <see cref="Id"/> class.
        /// </summary>
        /// <param name="value">The string value of the identifier</param>
        public Id(string value)
        {
            Value = value;
        }

        /// <summary>
        /// /// Creates a new Id instance with a new GUID if the provided value is null or empty.
        /// </summary>
        /// <param name="value">The value of the Id, pass null for a random GUID </param>
        /// <returns></returns>
        public static Id Create(string? value)
        {
            return new Id(value ?? Guid.NewGuid().ToString());
        }
       
        /// <summary>
        /// Returns true if the Id is null or empty.
        /// </summary>
        /// <param name="id">The id to test</param>
        /// <returns></returns>
        public static bool IsNullOrEmpty(Id? id)
        {
            return id == null || string.IsNullOrEmpty(id.Value);
        }

        /// <summary>
        /// Implicitly converts an Id to its string representation.
        /// </summary>
        /// <param name="id">The Id to convert</param>
        public static implicit operator string(Id id) => id.Value;

        /// <summary>
        /// Implicitly converts a string to an Id.
        /// </summary>
        /// <param name="value">The string to convert</param>
        public static implicit operator Id(string value) => new(value);

        /// <summary>
        /// Returns the string representation of the identifier.
        /// </summary>
        /// <returns>The identifier's string value</returns>
        public override string ToString() => Value;
    }
}
