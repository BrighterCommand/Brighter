using Google.Cloud.PubSub.V1;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter.MessagingGateway.GcpPubSub;


/// <summary>
/// A factory class responsible for creating concrete implementations of <see cref="IAmAMessageProducer"/>
/// for Google Cloud Pub/Sub.
/// It handles the creation of the underlying <see cref="Google.Cloud.PubSub.V1.PublisherClient"/> for each configured publication
/// and ensures the necessary topics exist on the Google Cloud platform.
/// </summary>
public class GcpPubSubMessageProducerFactory : GcpPubSubMessageGateway, IAmAMessageProducerFactory
{
    private readonly InstrumentationOptions? _instrumentation;
    private readonly GcpMessagingGatewayConnection _connection;
    private readonly IEnumerable<GcpPublication> _publications;

    /// <summary>
    /// Initializes a new instance of the <see cref="GcpPubSubMessageProducerFactory"/> class.
    /// </summary>
    /// <param name="connection">The connection details for the Google Cloud Pub/Sub gateway.</param>
    /// <param name="publications">A collection of Google Cloud Pub/Sub specific publication configurations.</param>
    /// <param name="instrumentation">Optional instrumentation options for tracing and metrics.</param>
    public GcpPubSubMessageProducerFactory(GcpMessagingGatewayConnection connection,
        IEnumerable<GcpPublication> publications,
        InstrumentationOptions? instrumentation = null) : base(connection)
    {
        _connection = connection;
        _publications = publications;
        _instrumentation = instrumentation;
    }

    /// <summary>
    /// Creates a dictionary of synchronous message producers, keyed by their <see cref="ProducerKey"/>.
    /// This method is a synchronous wrapper for <see cref="CreateAsync"/>.
    /// </summary>
    /// <returns>A dictionary where the key is the topic/channel name and the value is the message producer.</returns>
    public Dictionary<ProducerKey, IAmAMessageProducer> Create()
    {
        return BrighterAsyncContext.Run(() => CreateAsync());
    }

    /// <summary>
    /// Asynchronously creates a dictionary of message producers, keyed by their <see cref="ProducerKey"/>.
    /// </summary>
    /// <returns>A task that returns a dictionary where the key is the topic/channel name and the value is the message producer.</returns>
    /// <exception cref="ConfigurationException">Thrown if a publication is missing a topic.</exception>
    public async Task<Dictionary<ProducerKey, IAmAMessageProducer>> CreateAsync()
    {
        var producers = new Dictionary<ProducerKey, IAmAMessageProducer>();

        foreach (var publication in _publications)
        {
            if (RoutingKey.IsNullOrEmpty(publication.Topic))
            {
                throw new ConfigurationException("Missing topic on Publication");
            }

            // Initialize TopicAttributes if null and ensure the Topic Name is set
            publication.TopicAttributes ??= new TopicAttributes();
            if (string.IsNullOrEmpty(publication.TopicAttributes.Name))
            {
                publication.TopicAttributes.Name = publication.Topic;
            }

            // Ensure the Google Cloud Pub/Sub Topic exists, creating it if configured to do so
            await EnsureTopicExistAsync(publication.TopicAttributes, publication.MakeChannels);

            // Get the canonical Google Pub/Sub TopicName
            var topicName = GetTopicName(publication.TopicAttributes.ProjectId, publication.TopicAttributes.Name);

            // Create the Google PublisherClient which is used to send messages
            var client = await CreatePublisherClient(topicName, 
                publication.EnableMessageOrdering,
                publication.PublisherClientConfiguration ?? _connection.PublisherConfiguration);

            // Create the Brighter-specific producer wrapper and add it to the dictionary
            producers[new ProducerKey(publication.Topic, publication.Type)] = new GcpMessageProducer(
                client,
                publication,
                _instrumentation ?? InstrumentationOptions.None
            );
        }

        return producers;
    }

    private async Task<PublisherClient> CreatePublisherClient(TopicName topicName, 
        bool enableMessageOrdering,
        Action<PublisherClientBuilder>? configure)
    {
        var builder = new PublisherClientBuilder
        {
            Credential = _connection.Credential,
            TopicName = topicName,
            Settings = new PublisherClient.Settings
            {
                EnableMessageOrdering = enableMessageOrdering
            }
        };
        
        configure?.Invoke(builder);
        return await builder.BuildAsync();
    }
}
