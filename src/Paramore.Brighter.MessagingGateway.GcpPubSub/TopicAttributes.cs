using Google.Api.Gax;
using Google.Cloud.PubSub.V1;

namespace Paramore.Brighter.MessagingGateway.GcpPubSub;

public class TopicAttributes
{
    /// <summary>
    /// The Topic Name
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

    /// <summary>
    /// Enable message ordering.
    /// </summary>
    public bool EnableMessageOrdering { get; set; }

    /// <summary>
    /// The endpoint to connect to, or null to use the default endpoint.
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// The <see cref="Google.Api.Gax.BatchingSettings"/> that control how messages are batched when sending.
    /// </summary>
    public BatchingSettings? BatchSettings { get; set; }

    /// <summary>
    /// Enables publish message compression. If set to <c>true</c>, messages will be compressed before being sent to the server
    /// </summary>
    public bool EnableCompression { get; set; }

    /// <summary>
    /// Specifies the threshold for the number of bytes in a message batch before compression is applied.
    /// This property comes into play only when <see cref="EnableCompression"/> is set to <c>true</c>.
    /// If the number of bytes in a batch is less than this value, compression will not be applied even
    /// if <see cref="EnableCompression"/> is <c>true</c>.
    /// </summary>
    public long? CompressBytesThreshold { get; set; }
    
    /// <summary>
    /// What do we do with infrastructure dependencies for the producer?
    /// </summary>
    public OnMissingChannel MakeChannels { get; set; }

    internal static TopicAttributes FromPubSubPublication(PubSubPublication publication)
    {
        return new TopicAttributes
        {
            Name = publication.Topic!.Value,
            ProjectId = publication.ProjectId,
            Labels = publication.Labels,
            MessageRetentionDuration = publication.MessageRetentionDuration,
            StorePolicy = publication.StorePolicy,
            SchemaSettings = publication.SchemaSettings,
            KmsKeyName = publication.KmsKeyName,
            EnableMessageOrdering = publication.EnableMessageOrdering,
            Endpoint = publication.Endpoint,
            BatchSettings = publication.BatchSettings,
            EnableCompression = publication.EnableCompression,
            CompressBytesThreshold = publication.CompressBytesThreshold,
            MakeChannels = publication.MakeChannels
        };
    }
}
