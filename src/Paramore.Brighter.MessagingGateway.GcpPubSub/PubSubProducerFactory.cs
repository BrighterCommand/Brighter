using Google.Apis.Auth.OAuth2;
using Google.Cloud.PubSub.V1;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter.MessagingGateway.GcpPubSub;

/// <summary>
/// The Google Cloud PubSub producer factory.
/// </summary>
public class PubSubProducerFactory : PubSubMessageGateway, IAmAMessageProducerFactory
{
    private readonly GcpMessagingGatewayConnection _connection;
    private readonly IEnumerable<PubSubPublication> _publications;

    /// <summary>
    /// The Google Cloud PubSub producer factory.
    /// </summary>
    /// <param name="connection">The <see cref="GcpMessagingGatewayConnection"/>.</param>
    /// <param name="publications">The collection of <see cref="PubSubPublication"/>.</param>
    public PubSubProducerFactory(GcpMessagingGatewayConnection connection,
        IEnumerable<PubSubPublication> publications) : base(connection)
    {
        _connection = connection;
        _publications = publications;
    }

    /// <inheritdoc />
    public Dictionary<RoutingKey, IAmAMessageProducer> Create()
        => BrighterAsyncContext.Run(async () => await CreateAsync());

    /// <inheritdoc />
    public async Task<Dictionary<RoutingKey, IAmAMessageProducer>> CreateAsync()
    {
        var producers = new Dictionary<RoutingKey, IAmAMessageProducer>();

        foreach (var publication in _publications)
        {
            if (RoutingKey.IsNullOrEmpty(publication.Topic))
            {
                throw new ConfigurationException("Missing topic on Publication");
            }

            await EnsureTopicExistAsync(TopicAttributes.FromPubSubPublication(publication));
            var publisher = await new PublisherClientBuilder
            {
                Credential = new ComputeCredential(),
                TopicName = TopicName.FromProjectTopic(publication.ProjectId ?? _connection.ProjectId, publication.Topic),
                Endpoint = publication.Endpoint,
                Settings = new PublisherClient.Settings
                {
                    EnableMessageOrdering = publication.EnableMessageOrdering,
                    BatchingSettings = publication.BatchSettings,
                    EnableCompression = publication.EnableCompression,
                    CompressionBytesThreshold = publication.CompressBytesThreshold
                }
            }.BuildAsync();

            producers[publication.Topic] = new PubSubProducer(publisher, publication);
        }

        return producers;
    }
}
