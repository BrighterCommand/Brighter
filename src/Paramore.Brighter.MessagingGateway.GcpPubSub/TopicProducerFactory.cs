using Google.Apis.Auth.OAuth2;
using Google.Cloud.PubSub.V1;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter.MessagingGateway.GcpPubSub;

/// <summary>
/// The Google Cloud PubSub producer factory.
/// </summary>
public class TopicProducerFactory : PubSubMessageGateway, IAmAMessageProducerFactory
{
    private readonly GcpMessagingGatewayConnection _connection;
    private readonly IEnumerable<TopicPublication> _publications;

    /// <summary>
    /// The Google Cloud PubSub producer factory.
    /// </summary>
    /// <param name="connection">The <see cref="GcpMessagingGatewayConnection"/>.</param>
    /// <param name="publications">The collection of <see cref="TopicPublication"/>.</param>
    public TopicProducerFactory(GcpMessagingGatewayConnection connection,
        IEnumerable<TopicPublication> publications) : base(connection)
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

            publication.TopicAttributes ??= new TopicAttributes();
            if (string.IsNullOrEmpty(publication.TopicAttributes.Name))
            {
                publication.TopicAttributes.Name = publication.Topic;
            }

            await EnsureTopicExistAsync(publication.TopicAttributes, publication.MakeChannels);
            producers[publication.Topic] = new TopicProducer(_connection, publication);
        }

        return producers;
    }
}
