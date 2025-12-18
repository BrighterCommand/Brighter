using Google.Cloud.PubSub.V1;
using Google.Protobuf.WellKnownTypes;

namespace Paramore.Brighter.MessagingGateway.GcpPubSub;

/// <summary>
/// Represents Google Cloud Pub/Sub specific attributes used to configure or create a Topic.
/// This includes metadata and advanced settings like retention, storage policy, and encryption.
/// </summary>
public class TopicAttributes
{
    /// <summary>
    /// Gets or sets the name of the Topic. This is the resource identifier within the Project.
    /// If not set, it defaults to the Brighter <see cref="Publication.Topic"/> name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Google Cloud Project ID where the Topic should be located.
    /// If null, the <see cref="GcpMessagingGatewayConnection.ProjectId"/> will be used.
    /// </summary>
    public string? ProjectId { get; set; }

    /// <summary>
    /// Gets or sets a dictionary of key-value pairs that are attached to the Topic as labels.
    /// Labels are typically used for organization, billing, and resource management.
    /// </summary>
    public Dictionary<string, string> Labels { get; set; } = new();

    /// <summary>
    /// Gets or sets the duration for which Pub/Sub retains messages published to the topic.
    /// If null, messages are retained for 7 days (the default maximum).
    /// Note: The value must be at least 10 minutes (600 seconds).
    /// </summary>
    public TimeSpan? MessageRetentionDuration { get; set; }

    /// <summary>
    /// Gets or sets the message storage policy configuration for the Topic.
    /// This defines which Google Cloud regions are allowed to store messages for this topic.
    /// </summary>
    public MessageStoragePolicy? StorePolicy { get; set; }

    /// <summary>
    /// Gets or sets the schema settings for the Topic.
    /// This is used to enforce a specific schema (like Avro or Protocol Buffers) on published messages.
    /// </summary>
    public SchemaSettings? SchemaSettings { get; set; }

    /// <summary>
    /// Gets or sets the Cloud KMS key name that is used to encrypt and decrypt messages published to the topic.
    /// This enables Customer-Managed Encryption Keys (CMEK).
    /// </summary>
    public string? KmsKeyName { get; set; }
    
    /// <summary>
    /// Action to configure the <see cref="Topic"/> object before it is used for creation or update.
    /// This allows for setting any property on the underlying Google Pub/Sub Topic object not exposed
    /// directly by <see cref="TopicAttributes"/>.
    /// </summary>
    public Action<Topic>? TopicConfiguration { get; set; }
    
    /// <summary>
    /// Action to configure the <see cref="FieldMask"/> used when updating a Pub/Sub Topic.
    /// This is required to explicitly tell the Pub/Sub API which fields are being changed.
    /// Use this to include fields configured via <see cref="TopicConfiguration"/> that aren't
    /// standard Brighter attributes.
    /// </summary>
    public Action<FieldMask>? UpdateMaskConfiguration { get; set; }
}
