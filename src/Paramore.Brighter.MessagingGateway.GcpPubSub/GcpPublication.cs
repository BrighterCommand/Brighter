using Google.Cloud.PubSub.V1;

namespace Paramore.Brighter.MessagingGateway.GcpPubSub;

/// <summary>
/// Represents Google Cloud Pub/Sub specific configuration for a message publication (a topic).
/// This extends the core Brighter <see cref="Publication"/> with GCP-specific settings.
/// </summary>
public class GcpPublication : Publication
{
    /// <summary>
    /// Gets or sets the attributes for the Google Cloud Pub/Sub Topic.
    /// This includes settings such as the ProjectId, Topic Name, and potentially more advanced configurations.
    /// </summary>
    public TopicAttributes? TopicAttributes { get; set; }
    
    /// <summary>
    /// Gets or sets a value indicating whether messages published to the topic are delivered in the order they were published,
    /// provided they were published with an ordering key.
    /// </summary>
    public bool EnableMessageOrdering { get; set; }

    /// <summary>
    /// Gets or sets an <see cref="Action"/> to allow advanced customization of the <see cref="PublisherClientBuilder"/>.
    /// This is used to configure the client that publishes messages to the topic, for scenarios like
    /// setting custom client options, retries, or deadlines.
    /// </summary>
    public Action<PublisherClientBuilder>? PublisherClientConfiguration { get; set; }
}


/// <summary>
/// Represents Google Cloud Pub/Sub specific configuration for a message publication, strongly typed to a request type.
/// This allows for easy association between a Brighter <typeparamref name="T"/> and its publication settings.
/// </summary>
/// <typeparam name="T">The type of <see cref="IRequest"/> associated with this publication.</typeparam>
public class GcpPublication<T> : GcpPublication where T: IRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GcpPublication{T}"/> class,
    /// automatically setting the <see cref="Publication.RequestType"/> to <typeparamref name="T"/>.
    /// </summary>
    public GcpPublication()
    {
        RequestType = typeof(T);
    }
}
