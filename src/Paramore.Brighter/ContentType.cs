namespace Paramore.Brighter
{
    /// <summary>
    /// Represents a MIME content type as defined in RFC 2046.
    /// Used to indicate the media type of message content, allowing recipients to properly interpret the message payload.
    /// </summary>
    /// <remarks>
    /// Content types follow the format "type/subtype" as specified in RFC 2046.
    /// Common examples include "text/plain", "application/json", "application/xml".
    /// </remarks>
    public record ContentType
    {
        /// <summary>
        /// Gets the string representation of the content type.
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ContentType"/> class.
        /// </summary>
        /// <param name="value">The content type string in RFC 2046 format (e.g., "text/plain")</param>
        public ContentType(string value)
        {
            Value = value;
        }

        /// <summary>
        /// Implicitly converts a ContentType to its string representation.
        /// </summary>
        /// <param name="contentType">The ContentType to convert</param>
        public static implicit operator string(ContentType contentType) => contentType.Value;

        /// <summary>
        /// Implicitly converts a string to a ContentType.
        /// </summary>
        /// <param name="value">The string to convert</param>
        public static implicit operator ContentType(string value) => new(value);

        /// <summary>
        /// Returns the string representation of the content type.
        /// </summary>
        /// <returns>The content type string value</returns>
        public override string ToString() => Value;

        /// <summary>
        /// Represents the standard "text/plain" content type.
        /// </summary>
        public static ContentType TextPlain => new("text/plain");
        
        /// <summary>
        /// Represents the standard "application/json" content type.
        /// </summary>
        public static ContentType ApplicationJson=> new("application/json");
        
        /// <summary>
        /// Represents the standard "application/octet-stream" content type.
        /// </summary>
        public static ContentType OctetStream => new("application/octet");

        /// <summary>
        /// Determines if the provided ContentType is null or empty.
        /// </summary>
        /// <param name="contentType">The content type under test</param>
        /// <returns></returns>
        public static bool IsNullOrEmpty(ContentType? contentType)
        {
           return contentType == null || string.IsNullOrEmpty(contentType.Value);
        }
    }
}
