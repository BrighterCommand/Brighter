namespace Paramore.Brighter.MessagingGateway.GcpPubSub;

/// <summary>
/// The Google Cloud Pub/Sub publication
/// </summary>
public class PubSubPublication : Publication
{
    /// <summary>
    /// The attributes of the topic
    /// </summary>
    public required TopicAttributes TopicAttributes { get; init; }

    /// <summary>
    /// The max batch size for publish
    /// </summary>
    public int BatchSize { get; set; } = 1_000;
}
