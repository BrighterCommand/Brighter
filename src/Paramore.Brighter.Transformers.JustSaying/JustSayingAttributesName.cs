namespace Paramore.Brighter.Transformers.JustSaying;

/// <summary>
/// Contains constants defining header attribute names used by Brighter's JustSaying integration.
/// These attributes are attached to messages for routing and metadata purposes.
/// </summary>
public static class JustSayingAttributesName
{
    /// <summary>
    /// The common prefix for all JustSaying-related headers added by Brighter.
    /// Value: "Brighter-JustSaying-"
    /// </summary>
    public const string HeaderPrefix = "Brighter-JustSaying-";

    /// <summary>
    /// Identifies the component that raised/published the message.
    /// Format: "{HeaderPrefix}-Raising-Component"
    /// </summary>
    public const string RaisingComponent = $"{HeaderPrefix}-Raising-Component";

    /// <summary>
    /// Specifies the subject/topic where the message should be published.
    /// Format: "{HeaderPrefix}-Subject"
    /// </summary>
    public const string Subject = $"{HeaderPrefix}-Subject";

    /// <summary>
    /// Identifies the tenant context for the message (used in multi-tenant systems).
    /// Format: "{HeaderPrefix}-Tenant"
    /// </summary>
    public const string Tenant = $"{HeaderPrefix}-Tenant";

    /// <summary>
    /// Indicates the version of the message schema.
    /// Format: "{HeaderPrefix}-Version"
    /// </summary>
    public const string Version = $"{HeaderPrefix}-Version";
    
    /// <summary>
    /// The conversation that raised/published the message.
    /// Format: "{HeaderPrefix}-Raising-Component"
    /// </summary>
    public const string Conversation = $"{HeaderPrefix}-Conversation";

    /// <summary>
    /// Identifies the body encoding applied by JustSaying when publish-side compression is enabled.
    /// JustSaying sets this as an SNS/SQS message attribute (key: <c>"Content-Encoding"</c>) with a value
    /// of <see cref="GzipBase64ContentEncoding"/> when the payload exceeds the configured threshold.
    /// </summary>
    public const string ContentEncoding = "Content-Encoding";

    /// <summary>
    /// JustSaying's gzip + base64 content-encoding marker. Matches
    /// <c>JustSaying.Messaging.Compression.ContentEncodings.GzipBase64</c>.
    /// </summary>
    public const string GzipBase64ContentEncoding = "gzip,base64";
}
