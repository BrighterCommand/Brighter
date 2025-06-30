namespace Paramore.Brighter.MessagingGateway.GcpPubSub;

/// <summary>
/// The Google Cloud Pub/Sub publication
/// </summary>
public class GcpPublication : Publication
{
    /// <summary>
    /// The attributes of the topic
    /// </summary>
    public TopicAttributes? TopicAttributes { get; set; }

    /// <summary>
    /// The max batch size for publish
    /// </summary>
    public int BatchSize { get; set; } = 1_000;
}


/// <summary>
/// The Google Cloud Pub/Sub publication
/// </summary>
public class GcpPublication<T> : GcpPublication
{
    public GcpPublication()
    {
        RequestType = typeof(T);
    }
}
