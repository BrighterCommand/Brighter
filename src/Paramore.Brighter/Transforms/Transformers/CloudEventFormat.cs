namespace Paramore.Brighter.Transforms.Transformers;

/// <summary>
/// Specifies the format used for encoding CloudEvents messages, typically
/// when mapping to/from transport messages (e.g., Pub/Sub messages).
/// </summary>
/// <remarks>
/// This determines how the CloudEvents attributes (like 'id', 'source', 'type')
/// and the event data itself are represented within the message payload or headers.
/// See the <see href="https://github.com/cloudevents/spec/blob/v1.0.2/cloudevents/spec.md#event-formats">CloudEvents Specification - Event Formats</see>
/// for more details.
/// </remarks>
public enum CloudEventFormat
{
    /// <summary>
    /// Indicates the CloudEvents Binary Content Mode.
    /// In this mode, event metadata attributes are typically mapped to transport-specific
    /// metadata (like message headers or properties), and the event 'data' is carried
    /// directly in the message body, preserving its original media type (e.g., application/json, text/plain).
    /// </summary>
    Binary,

    /// <summary>
    /// Indicates the CloudEvents Structured Content Mode using JSON format.
    /// In this mode, the entire CloudEvent, including all metadata attributes and the
    /// event 'data', is encoded as a single JSON object within the message body.
    /// The message's content type is typically set to 'application/cloudevents+json'.
    /// </summary>
    Json
}
