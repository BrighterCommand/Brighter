using Google.Cloud.PubSub.V1;

namespace Paramore.Brighter.MessagingGateway.GcpPubSub;

/// <summary>
/// The topic attribute
/// </summary>
public class TopicAttributes
{
    /// <summary>
    /// The topic name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The project ID
    /// </summary>
    /// <remarks>
    /// This property has priority over <see cref="GcpMessagingGatewayConnection.ProjectId"/>
    /// </remarks>
    public string? ProjectId { get; set; }

    /// <summary>
    /// The topic label
    /// </summary>
    /// <remarks>
    /// it's used when Brighter is creating the label
    /// </remarks>
    public Dictionary<string, string> Labels { get; set; } = new();

    /// <summary>
    /// The Message retention duration option specifies how long Pub/Sub retains messages after publication.
    /// After the message retention duration passes, Pub/Sub might discard the message independent of the
    /// acknowledgment state of the message. 
    /// </summary>
    public TimeSpan? MessageRetentionDuration { get; set; }

    /// <summary>
    /// Policy constraining the set of Google Cloud Platform regions
    /// where messages published to the topic may be stored.
    /// </summary>
    public MessageStoragePolicy? StorePolicy { get; set; }

    /// <summary>
    /// The <see cref="Google.Cloud.PubSub.V1.SchemaSettings"/> for validating messages published against a schema.
    /// </summary>
    public SchemaSettings? SchemaSettings { get; set; }

    /// <summary>
    /// The resource name of the Cloud KMS CryptoKey to be used to
    /// protect access to messages published on this topic.
    /// </summary>
    public string? KmsKeyName { get; set; }
}
